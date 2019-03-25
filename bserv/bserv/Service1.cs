using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.IO;
using System.Linq;
using System.Management;
using System.Collections.ObjectModel;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Reflection;
using SDDUSB;
using System.Security.Cryptography;

namespace bserv
{

    public partial class Service1 : ServiceBase
    {
        bool sistempe64biti = Environment.Is64BitOperatingSystem;
        string numedevconpe32sau64biti;
        Process rularecmd = new Process();
        string caleSalvareListaDispozUSB= System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\fdpsdbu.dat";
        string caleSalvareLoguri = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\log.txt";
        ObservableCollection<DispozitivUSB> listaDispUSB;
        ObservableCollection<DispozitivUSB> listaTemp = new ObservableCollection<DispozitivUSB>();
        SerializeUSB serializaresideserializare = new SerializeUSB();
        ManagementEventWatcher watcher = new ManagementEventWatcher();
        ManagementEventWatcher watcher2 = new ManagementEventWatcher();

        public Service1()
        {
            InitializeComponent();
            if (sistempe64biti)
            {
                numedevconpe32sau64biti = "devconx64.exe";
            }
            else
            {
                numedevconpe32sau64biti = "devconx86.exe";
            }
        }

        protected override void OnStart(string[] args)
        {
            //se ruleaza o scanare initiala a dispozitivelor USB la pornirea serviciului pentru compararea dispozitivelor conectate in sistem cu lista serializata 
            ScanareInitialaListaDispozitiveUSB();

            WqlEventQuery query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent " + "WITHIN 2 " + "WHERE TargetInstance ISA 'Win32_USBControllerDevice'");
            watcher.EventArrived += new EventArrivedEventHandler(ScanareDispozUSB);
            watcher.Query = query;
            watcher.Start();

            WqlEventQuery query2 = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent " + "WITHIN 2 " + "WHERE TargetInstance ISA 'Win32_USBControllerDevice'");
            watcher2.EventArrived += new EventArrivedEventHandler(DeinregistrareDispozUSB);
            watcher2.Query = query2;
            watcher2.Start();

            //se notifica in loguri pornirea serviciului
            File.AppendAllText(caleSalvareLoguri, $"Serviciul de blocare/deblocare dispozitive USB s-a pornit la {DateTime.Now}\r\n");
        }

        protected override void OnStop()
        {
            File.AppendAllText(caleSalvareLoguri, $"Serviciul deblocare/deblocare dispozitive USB s-a oprit la {DateTime.Now}\r\n");
        }

        protected override void OnCustomCommand(int command)
        {
            if (command == 128)
            {
                ScanareInitialaListaDispozitiveUSB();
            }
        }

        private void ScanareDispozUSB(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string devconID = instance.GetPropertyValue("Dependent") as string;
            if (devconID != null)
            {
                devconID = devconID.Remove(devconID.LastIndexOf('\"'));
                devconID = devconID.Substring(devconID.LastIndexOf('\"')+1);
                devconID = devconID.Replace("\\\\", "\\");
                File.AppendAllText(caleSalvareLoguri, "Dispozitivul " + devconID + $" s-a conectat la {DateTime.Now}"+ "\r\n");
                DispozitivUSB dispozitivGasit = null;
                foreach(DispozitivUSB dispozitivDeCautat in listaTemp)
                {
                    if(dispozitivDeCautat.DevconID.Contains(devconID))
                    {
                        dispozitivGasit = dispozitivDeCautat;
                    }
                }
                if (dispozitivGasit!=null)
                {
                    rularecmd.StartInfo.FileName = numedevconpe32sau64biti;
                    rularecmd.StartInfo.Arguments = $"status \"@{dispozitivGasit.DevconID}\"";
                    rularecmd.StartInfo.UseShellExecute = false;
                    rularecmd.StartInfo.RedirectStandardOutput = true;
                    rularecmd.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    rularecmd.Start();
                    string rezultatStatus = rularecmd.StandardOutput.ReadToEnd();
                    if (dispozitivGasit.Stare == false && rezultatStatus.Contains("running"))
                    {
                        rularecmd.StartInfo.FileName = numedevconpe32sau64biti;
                        rularecmd.StartInfo.Arguments = $"disable \"@{dispozitivGasit.DevconID}\"";
                        rularecmd.StartInfo.UseShellExecute = false;
                        rularecmd.StartInfo.RedirectStandardOutput = true;
                        rularecmd.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                        rularecmd.Start();
                        string output2 = rularecmd.StandardOutput.ReadToEnd();
                        if(output2.Contains("Disabled on reboot"))
                        {
                            rularecmd.StartInfo.Arguments = $"restart \"@{dispozitivGasit.DevconID}\"";
                            rularecmd.Start();
                        }
                    }
                    else if (dispozitivGasit.Stare == true && rezultatStatus.Contains("disabled"))
                    {
                        rularecmd.StartInfo.FileName = numedevconpe32sau64biti;
                        rularecmd.StartInfo.Arguments = $"enable \"@{dispozitivGasit.DevconID}\"";
                        rularecmd.StartInfo.UseShellExecute = false;
                        rularecmd.StartInfo.RedirectStandardOutput = true;
                        rularecmd.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                        rularecmd.Start();
                        string output3 = rularecmd.StandardOutput.ReadToEnd();
                        if (output3.Contains("Disabled on reboot"))
                        {
                            rularecmd.StartInfo.Arguments = $"restart \"@{dispozitivGasit.DevconID}\"";
                            rularecmd.Start();
                        }
                    }
                    string idConectat = dispozitivGasit.DevconID;
                    
                    //se inregistreaza in conectarea dispozitivului in fisierul de date
                    while (true)
                    {
                        try
                        {
                            if (File.Exists(caleSalvareListaDispozUSB))
                            {
                                using (FileStream fStream1 = new FileStream(caleSalvareListaDispozUSB, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                                {
                                    if (fStream1.Length > 0)
                                    {
                                        listaTemp = serializaresideserializare.Deserializare(fStream1);
                                        foreach (DispozitivUSB dispTempUSB in listaTemp)
                                        {
                                            if (dispTempUSB.DevconID.Contains(idConectat))
                                            {
                                                dispTempUSB.Conectare = Conectare.Conectat;
                                            }
                                        }
                                        //se goleste fisierul de date cu ajutorul urmatoarelor 3 comenzi
                                        fStream1.SetLength(0);
                                        fStream1.Position = 0;
                                        fStream1.Flush();

                                        // Se reserializeaza datele in format binar
                                        serializaresideserializare.Serializare(fStream1, listaTemp);
                                        return;
                                    }
                                    else
                                    {
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                return;
                            }

                        }
                        catch (IOException exceptie)
                        {
                            File.AppendAllText(caleSalvareLoguri, "A)"+ DateTime.Now.ToString() + exceptie.Message + "\r\n");
                            Thread.Sleep(500);
                        }
                    }
                }
                else
                {
                    //daca nu este gasit dispozitivul introdus in lista de dispozitive atunci se ruleaza o scanarea initiala care va descoperii
                    //dispozitivele USB nou introduse si le va seta starea ca disabled
                    ScanareInitialaListaDispozitiveUSB();
                }

            }
        }

        private void ScanareInitialaListaDispozitiveUSB()
        {
            //se ruleaza mai intai scanarea initiala pentru a determina dispozitivele USB conectate
            ScanareDispozitiveUSBConectate();    

            //se deserializeaza lista de dispozitive salvate si se compara, se modifica la nevoie dupa care se reserializeaza
            while (true)
            {
                try
                {
                    // Se deserializeaza datele din format binar pentru a fi comparate si daca e nevoie se vor retine si dispozitivele care nu sunt in prezent in sistem
                    if (File.Exists(caleSalvareListaDispozUSB))
                    {
                        using (FileStream fStream = new FileStream(caleSalvareListaDispozUSB, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            if(fStream.Length>0) //se detecteaza daca fisierul cu date este gol
                            {
                                //se detecteaza daca s-au adaugat dispozitive USB noi in sistem
                                listaTemp = serializaresideserializare.Deserializare(fStream);
                                ObservableCollection<DispozitivUSB> listaDispNoi = new ObservableCollection<DispozitivUSB>();
                                foreach(DispozitivUSB dispozitivScanat in listaDispUSB)
                                {
                                    bool dispExistent = false;
                                    foreach(DispozitivUSB dispozitivSalvat in listaTemp)
                                    {
                                        if(dispozitivSalvat.ID==dispozitivScanat.ID&&dispozitivSalvat.DevconID==dispozitivScanat.DevconID&&dispozitivSalvat.PNPDeviceID==dispozitivScanat.PNPDeviceID)
                                        {
                                            dispExistent = true;
                                        }

                                    }
                                    if(!dispExistent)
                                    {
                                        listaDispNoi.Add(dispozitivScanat);
                                    }
                                }
                                foreach(DispozitivUSB dispozitivNou in listaDispNoi)
                                {
                                    listaTemp.Add(dispozitivNou);
                                }

                                //se verifica starea dispozitivelor
                                List<string> listafinala = new List<string>();
                                rularecmd.StartInfo.FileName = numedevconpe32sau64biti;
                                rularecmd.StartInfo.Arguments = "status =USB"; 
                                rularecmd.StartInfo.UseShellExecute = false;
                                rularecmd.StartInfo.RedirectStandardOutput = true;
                                rularecmd.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                                rularecmd.Start();
                                string output = rularecmd.StandardOutput.ReadToEnd();
                                if(!(output.Contains("No matching devices found.")))
                                {
                                    output = output.Remove(output.LastIndexOf('.'));
                                    output = output.Remove(output.LastIndexOf('.'));
                                    string[] lista = output.Split('.');
                                    foreach (string element in lista)
                                    {
                                        listafinala.Add(element);
                                    }
                                }
                               
                                rularecmd.StartInfo.FileName = numedevconpe32sau64biti;
                                rularecmd.StartInfo.Arguments = "status =WPD";
                                rularecmd.StartInfo.UseShellExecute = false;
                                rularecmd.StartInfo.RedirectStandardOutput = true;
                                rularecmd.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                                rularecmd.Start();
                                string output1 = rularecmd.StandardOutput.ReadToEnd();
                                if(!(output1.Contains("No matching devices found.")))
                                {
                                    output1 = output1.Remove(output1.LastIndexOf('.'));
                                    output1 = output1.Remove(output1.LastIndexOf('.'));
                                    string[] lista1 = output1.Split('.');
                                    foreach (string element1 in lista1)
                                    {
                                        listafinala.Add(element1);
                                    }
                                }

                                //se activeaza sau dezactiveaza dispozitive USB in conformitate cu lista serializata
                                foreach(string stringStare in listafinala)
                                {
                                    foreach(DispozitivUSB dispUSB in listaTemp)
                                    {
                                        if (stringStare.Contains(dispUSB.DevconID))
                                        {
                                            if(dispUSB.Stare==false&&stringStare.Contains("running"))
                                            {
                                                rularecmd.StartInfo.FileName = numedevconpe32sau64biti;
                                                rularecmd.StartInfo.Arguments = $"disable \"@{dispUSB.DevconID}\"";
                                                rularecmd.StartInfo.UseShellExecute = false;
                                                rularecmd.StartInfo.RedirectStandardOutput = true;
                                                rularecmd.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                                                rularecmd.Start();
                                                string output2 = rularecmd.StandardOutput.ReadToEnd();
                                                if (output2.Contains("Disabled on reboot"))
                                                {
                                                    rularecmd.StartInfo.Arguments = $"restart \"@{dispUSB.DevconID}\"";
                                                    rularecmd.Start();
                                                }
                                            }
                                            else if(dispUSB.Stare == true && stringStare.Contains("disabled"))
                                            {
                                                rularecmd.StartInfo.FileName = numedevconpe32sau64biti;
                                                rularecmd.StartInfo.Arguments = $"enable \"@{dispUSB.DevconID}\"";
                                                rularecmd.StartInfo.UseShellExecute = false;
                                                rularecmd.StartInfo.RedirectStandardOutput = true;
                                                rularecmd.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                                                rularecmd.Start();
                                                string output3 = rularecmd.StandardOutput.ReadToEnd();
                                                if (output3.Contains("Disabled on reboot"))
                                                {
                                                    rularecmd.StartInfo.Arguments = $"restart \"@{dispUSB.DevconID}\"";
                                                    rularecmd.Start();
                                                }
                                            }
                                        }
                                    }
                                }

                                //se goleste fisierul de date cu ajutorul urmatoarelor 3 comenzi
                                fStream.SetLength(0);
                                fStream.Position = 0;
                                fStream.Flush();

                                // Se reserializeaza datele in format binar
                                serializaresideserializare.Serializare(fStream, listaTemp);
                                return;
                            }
                            else
                            {
                                listaTemp = listaDispUSB;

                                // Se reserializeaza datele in format binar
                                serializaresideserializare.Serializare(fStream, listaTemp);
                                return;
                            }  
                        }
                    }
                    else
                    {
                        FileStream fStream = File.Create(caleSalvareListaDispozUSB);
                        fStream.Close();
                    }
                }
                catch (Exception exceptie)
                {
                    File.AppendAllText(caleSalvareLoguri, "B)" + DateTime.Now.ToString() + exceptie.Message + "\r\n");
                    Thread.Sleep(500);
                }

            }
        }

        private void DeinregistrareDispozUSB(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string devconID = instance.GetPropertyValue("Dependent") as string;
            if (devconID != null)
            {
                devconID = devconID.Remove(devconID.LastIndexOf('\"'));
                devconID = devconID.Substring(devconID.LastIndexOf('\"') + 1);
                devconID = devconID.Replace("\\\\", "\\");
                File.AppendAllText(caleSalvareLoguri, "Dispozitivul " + devconID + $" s-a deconectat la {DateTime.Now}" + "\r\n");
            }
        }

        private void ScanareDispozitiveUSBConectate()
        {
            try
            {
                //se obtin mai intai dispozitivele usb cu devcon
                listaDispUSB = new ObservableCollection<DispozitivUSB>();
                rularecmd.StartInfo.FileName = numedevconpe32sau64biti;
                rularecmd.StartInfo.Arguments = "listclass USB"; 
                rularecmd.StartInfo.UseShellExecute = false;
                rularecmd.StartInfo.RedirectStandardOutput = true;
                rularecmd.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                rularecmd.Start();
                string output = rularecmd.StandardOutput.ReadToEnd();
                File.AppendAllText(caleSalvareLoguri, $"USB output={output} \r\n");
                output = output.TrimEnd('\n', ' ', '\r');
                output = output.Substring(output.IndexOf('\\') - 3);
                string[] lista = output.Split('\r');
                if (lista.Count() > 0)
                {
                    lista[0] = lista[0].Substring(lista[0].IndexOf('\\') - 3);
                }
                foreach (string elem in lista)
                {
                    DispozitivUSB dispozitivUSB = new DispozitivUSB();

                    if (elem.Contains(":"))
                    {
                        dispozitivUSB.DevconID = elem.Substring(0, elem.IndexOf(":")).TrimEnd(' ').TrimStart('\n');
                        dispozitivUSB.Nume = dispozitivUSB.DevconID;
                        dispozitivUSB.ID = dispozitivUSB.DevconID.Substring(dispozitivUSB.DevconID.LastIndexOf('\\') + 1);
                        dispozitivUSB.Tip_dispozitiv = elem.Substring(elem.IndexOf(':') + 1);
                    }
                    listaDispUSB.Add(dispozitivUSB);
                }

                //se obtin apoi dispozitivele WPD (smarthphoneuri si alte dispozitive cu stocare conectate in modul mtp)
                rularecmd.StartInfo.FileName = numedevconpe32sau64biti;
                rularecmd.StartInfo.Arguments = "listclass WPD";
                rularecmd.StartInfo.UseShellExecute = false;
                rularecmd.StartInfo.RedirectStandardOutput = true;
                rularecmd.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                rularecmd.Start();
                string output1 = rularecmd.StandardOutput.ReadToEnd();
                File.AppendAllText(caleSalvareLoguri, $"WPD output={output1} \r\n");
                if (output1.Count() > 0)
                {
                    output1 = output1.TrimEnd('\n', ' ', '\r');
                    output1 = output1.Substring(output1.IndexOf('\\') - 3);
                }
                string[] lista1 = output1.Split('\r');
                foreach (string elem in lista1)
                {
                    DispozitivUSB dispozitivUSB = new DispozitivUSB();
                    if (elem.Contains(":"))
                    {
                        dispozitivUSB.DevconID = elem.Substring(0, elem.IndexOf(":")).TrimEnd(' ').TrimStart('\n');
                        dispozitivUSB.Nume = dispozitivUSB.DevconID;
                        dispozitivUSB.ID = dispozitivUSB.DevconID.Substring(dispozitivUSB.DevconID.LastIndexOf('\\') + 1);
                        dispozitivUSB.Tip_dispozitiv = elem.Substring(elem.IndexOf(':') + 1);
                    }
                    listaDispUSB.Add(dispozitivUSB);
                }
                File.AppendAllText(caleSalvareLoguri, $"Lista disp USB= \r\n");
                foreach(DispozitivUSB dispU in listaDispUSB)
                {
                    File.AppendAllText(caleSalvareLoguri, $"{dispU.DevconID} , {dispU.Nume} , {dispU.ID} , {dispU.Tip_dispozitiv}");
                }

                /* de implementat pe viitor recunoasterea capacitatii si a numelui pnpentity daca impactul asupra performantei nu e prea mare
                //obtinere date dispozitive USB cu ManagementObjectSearcher
                ManagementObjectCollection collection;
                using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_DiskDrive"))
                collection = searcher.Get();
                foreach (ManagementObject device in collection)
                {
                    string pnpDevID = device.GetPropertyValue("PNPDeviceID") as string;
                    if (pnpDevID != null)
                    {
                        foreach (DispozitivUSB dispozit in listaDispUSB)
                        {
                            if (pnpDevID.Contains(dispozit.ID))
                            {
                                dispozit.PNPDeviceID = pnpDevID;
                                if (device.GetPropertyValue("Size") is ulong)
                                {
                                    dispozit.Capacitate = ((ulong)device.GetPropertyValue("Size") / 1000000000).ToString() + "GB";
                                }
                                dispozit.Nume = device.GetPropertyValue("Model") as string;
                            }
                        }
                    }
                }
                collection.Dispose();
                */
            }
            catch (Exception exceptie)
            {
                File.AppendAllText(caleSalvareLoguri, "C)" + DateTime.Now.ToString() + exceptie.Message + "\r\n");
            }
        }
    }
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Management;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Windows.Forms;
using System.Threading;
using System.Reflection;
using SDDUSB;
using System.ServiceProcess;

namespace USB_Blocker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        string caleSalvareListaDispozUSB = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\fdpsdbu.dat";
        string caleSalvareLoguri = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\log.txt";
        SaveFileDialog salvareLoguri = new SaveFileDialog();
        ObservableCollection<DispozitivUSB> listaDispozUSB = new ObservableCollection<DispozitivUSB>();
        ObservableCollection<DispozitivUSB> listaTemp = new ObservableCollection<DispozitivUSB>();
        SerializeUSB serializaresideserializare = new SerializeUSB();
        ServiceController[] servicii;

        public MainWindow()
        {
            InitializeComponent();
            dataGridListaDispozUsb.DataContext = listaDispozUSB;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(caleSalvareLoguri))
            {
                textblockLoguri.Text = File.ReadAllText(caleSalvareLoguri);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            while(true)
            {
                try
                {
                    // Se deserializeaza datele din format binar pentru a fi comparate si daca e nevoie se vor retine si dispozitivele care nu sunt in prezent in sistem
                    if (File.Exists(caleSalvareListaDispozUSB))
                    {
                        using (FileStream fStream1 = new FileStream(caleSalvareListaDispozUSB, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            if (fStream1.Length > 0)
                            {
                                listaTemp = serializaresideserializare.Deserializare(fStream1);
                                for (int i = 0; i < listaDispozUSB.Count; i++)
                                {
                                    for (int j = 0; j < listaTemp.Count; j++)
                                    {
                                        if (listaTemp[j].ID == listaDispozUSB[i].ID && listaTemp[j].DevconID == listaDispozUSB[i].DevconID && listaTemp[j].PNPDeviceID == listaDispozUSB[i].PNPDeviceID)
                                        {

                                            listaTemp[j] = listaDispozUSB[i];
                                        }
                                    }
                                }
                                foreach (DispozitivUSB dispozitiv in listaDispozUSB)
                                {
                                    if (!listaTemp.Contains(dispozitiv))
                                    {
                                        listaTemp.Add(dispozitiv);
                                    }
                                }

                                //se goleste fisierul de date cu ajutorul urmatoarelor 3 comenzi
                                fStream1.SetLength(0);
                                fStream1.Position=0;
                                fStream1.Flush();

                                // Se reserializeaza datele in format binar
                                serializaresideserializare.Serializare(fStream1, listaTemp);

                                servicii = ServiceController.GetServices();
                                foreach(ServiceController serviciu in servicii)
                                {
                                    string numeServiciu = serviciu.ServiceName;
                                    if(serviciu.ServiceName=="bserv")
                                    {
                                        if(serviciu.Status==ServiceControllerStatus.Running)
                                        {
                                            serviciu.ExecuteCommand(128);
                                        }
                                        else
                                        {
                                            System.Windows.MessageBox.Show("Serviciul de blocare/deblocare nu ruleaza!");
                                        }
                                    }
                                }
                                return;
                            }
                            else
                            {
                                System.Windows.MessageBox.Show("Nu au fost descoperite dispozitive de stocare USB sau nu ruleaza serviciul de descoperire a dispozitivelor de stocare USB.");
                                return;
                            }
                        }
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Lista cu dispozitivele de stocare USB lipseste.");
                        return;
                    }

                }
                catch(IOException exceptie)
                {
                    File.AppendAllText(caleSalvareLoguri, "A)"+ DateTime.Now.ToString() + "Exceptie aplicatie:" + exceptie.Message + "\r\n");
                    Thread.Sleep(500);
                }
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            salvareLoguri.Filter = "Fisier txt (*.txt)|*.txt";
            salvareLoguri.RestoreDirectory = true;
            if (salvareLoguri.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (File.Exists(caleSalvareLoguri))
                {
                    File.AppendAllText(salvareLoguri.FileName, File.ReadAllText(caleSalvareLoguri));
                }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {

        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            // Se deserializeaza datele din format binar pentru afisare
            while (true)
            {
                try
                {
                    if (File.Exists(caleSalvareListaDispozUSB))
                    {
                        using (FileStream fStream1 = new FileStream(caleSalvareListaDispozUSB, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            if (fStream1.Length > 0)
                            {
                                listaDispozUSB = serializaresideserializare.Deserializare(fStream1);
                                dataGridListaDispozUsb.DataContext = listaDispozUSB; //se schimba contextul de date pt a semnalizarea schimbarea valorilor listei
                            }
                            else
                            {
                                System.Windows.MessageBox.Show("Nu au fost descoperite dispozitive de stocare USB sau nu ruleaza serviciul de descoperire a dispozitivelor de stocare USB.");
                                return;
                            }
                            return;
                        }
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Lista cu dispozitivele de stocare USB lipseste.");
                        return;
                    }
                }
                catch (IOException exceptie)
                {
                    File.AppendAllText(caleSalvareLoguri, "B)" + DateTime.Now.ToString() + "Exceptie aplicatie:" + exceptie.Message + "\r\n");
                    Thread.Sleep(500);
                }
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (File.Exists(caleSalvareLoguri))
            {
                FileStream fStream = File.Open(caleSalvareLoguri, FileMode.Truncate);
                fStream.Flush();
                fStream.Close();
                textblockLoguri.Text = File.ReadAllText(caleSalvareLoguri);
            }
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            string mesaj = File.ReadAllText(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\readme.txt");
            System.Windows.MessageBox.Show(mesaj);
        }
    }
}


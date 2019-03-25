using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System.IO;

namespace SDDUSB
{
    public class SerializeUSB
    {
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        public void Serializare(FileStream filestream, ObservableCollection<DispozitivUSB> lista)
        {
            binaryFormatter.Serialize(filestream, lista);
        }
        public ObservableCollection<DispozitivUSB> Deserializare(FileStream fileStream)
        {
            return binaryFormatter.Deserialize(fileStream) as ObservableCollection<DispozitivUSB>;
        }
    }

    [Serializable]
    public class DispozitivUSB
    {
        public string ID { get; set; }
        public string DevconID { get; set; }
        public string PNPDeviceID { get; set; }
        public string Nume { get; set; }
        public string Capacitate { get; set; }
        public string Tip_dispozitiv { get; set; }
        public bool Stare { get; set; }
        public Conectare Conectare { get; set; } = Conectare.Conectat;
    }

    public enum Conectare { Deconectat, Conectat, }
}

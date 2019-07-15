using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedDriveClient
{
    public class ProgressModel : INotifyPropertyChanged
    {
        private int pct_completed;
        private string filename;

        public int PctCompleted {
            get
            {
                return pct_completed;
            }
            set
            {
                pct_completed = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PctCompleted"));
            }
        }
        public string Filename {
            get
            {
                return filename;
            }
            set
            {
                filename = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Filename"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}

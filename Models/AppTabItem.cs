using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace ArcelikExcelApp.Models
{
    public class AppTabItem : INotifyPropertyChanged
    {
        private bool _isActive;
        private string _title;

        public string Tag { get; set; }
        public string Title 
        { 
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }
        public string Icon { get; set; }
        public UserControl Content { get; set; }
        public bool IsCloseable { get; set; } = true;

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

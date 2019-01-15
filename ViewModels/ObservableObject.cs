using System.ComponentModel;

namespace AncestryDnaClustering.ViewModels
{
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetFieldValue<T>(ref T field, T value, string fieldName)
        {
            if (ReferenceEquals(field, null) && ReferenceEquals(value, null))
            {
                return false;
            }

            if (ReferenceEquals(field, null) || !field.Equals(value))
            {
                field = value;
                OnPropertyChanged(fieldName);
                return true;
            }
            return false;
        }
    }
}

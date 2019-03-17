using System;
using System.Windows.Input;

namespace AncestryDnaClustering.ViewModels
{
    public class RelayCommand : RelayCommand<object>
    {
        public RelayCommand(Action execute)
            : base(param => execute()) { }

        public RelayCommand(Action execute, Func<bool> canExecute)
            : base(param => execute(), param => canExecute()) { }
    }

    public class RelayCommand<T> : ICommand
    {
        private Action<T> _execute;

        private Predicate<T> _canExecute;

        private event EventHandler CanExecuteChangedInternal;

        public RelayCommand(Action<T> execute)
            : this(execute, DefaultCanExecute)
        {
        }

        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
        }

        public event EventHandler CanExecuteChanged
        {
            add
            {
                CommandManager.RequerySuggested += value;
                CanExecuteChangedInternal += value;
            }

            remove
            {
                CommandManager.RequerySuggested -= value;
                CanExecuteChangedInternal -= value;
            }
        }

        public bool CanExecute(object parameter)
        {
            var result = _canExecute?.Invoke((T)parameter) == true;
            return result;
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        public void OnCanExecuteChanged()
        {
            var handler = CanExecuteChangedInternal;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public void Destroy()
        {
            _canExecute = _ => false;
            _execute = _ => { };
        }

        private static bool DefaultCanExecute(T parameter) => true;
    }
}

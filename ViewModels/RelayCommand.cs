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
        private Action<T> execute;

        private Predicate<T> canExecute;

        private event EventHandler CanExecuteChangedInternal;

        public RelayCommand(Action<T> execute)
            : this(execute, DefaultCanExecute)
        {
        }

        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
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
            var result = canExecute != null && canExecute((T)parameter);
            return result;
        }

        public void Execute(object parameter)
        {
            execute((T)parameter);
        }

        public void OnCanExecuteChanged()
        {
            EventHandler handler = CanExecuteChangedInternal;
            if (handler != null)
            {
                handler.Invoke(this, EventArgs.Empty);
            }
        }

        public void Destroy()
        {
            canExecute = _ => false;
            execute = _ => { return; };
        }

        private static bool DefaultCanExecute(T parameter)
        {
            return true;
        }
    }
}

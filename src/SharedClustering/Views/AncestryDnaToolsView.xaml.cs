using AncestryDnaClustering.ViewModels;
using System.Windows;

namespace AncestryDnaClustering
{
    /// <summary>
    /// Interaction logic for AncestryDnaClusteringView.xaml
    /// </summary>
    public partial class AncestryDnaToolsView : Window
    {
        public AncestryDnaToolsView()
        {
            InitializeComponent();
            DataContext = new AncestryDnaToolsViewModel(); 
        }
    }
}

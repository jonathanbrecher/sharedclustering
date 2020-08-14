using SharedClustering.ViewModels;
using System.Windows;

namespace SharedClustering
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

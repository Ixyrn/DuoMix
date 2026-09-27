using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace DuoMix;
static class MaterialFixture
{
    public static Window Create()
    {
        var grid = new Grid();
        for (int i = 0; i < 8; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var b = new Border { Background = new SolidColorBrush(i % 2 == 0 ? Color.FromRgb(240, 45, 60) : Color.FromRgb(20, 180, 245)) };
            Grid.SetColumn(b, i); grid.Children.Add(b);
        }
        return new Window { Title = "DuoMix glass test background", Width = 1000, Height = 850, WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = grid, ShowInTaskbar = false };
    }
}

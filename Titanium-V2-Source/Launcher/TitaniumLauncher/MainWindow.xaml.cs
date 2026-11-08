using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TitaniumLauncher.ViewModels;
namespace TitaniumLauncher;
public partial class MainWindow : Window
{
    public MainWindow():this(new MainViewModel()){}
    public MainWindow(MainViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

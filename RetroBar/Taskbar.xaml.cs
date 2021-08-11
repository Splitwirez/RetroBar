﻿#nullable enable
using ManagedShell.AppBar;
using ManagedShell.Common.Helpers;
using ManagedShell.Interop;
using ManagedShell;
using ManagedShell.WindowsTray;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using ManagedShell.ShellFolders;
using RetroBar.Utilities;
using Application = System.Windows.Application;

namespace RetroBar
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Taskbar : AppBarWindow
    {
        private bool _isReopening;
        private CloakMonitor _cloakMonitor;
        private ShellManager _shellManager;

        public Taskbar(ShellManager shellManager, CloakMonitor cloakMonitor, AppBarScreen screen, AppBarEdge edge)
            : base(shellManager.AppBarManager, shellManager.ExplorerHelper, shellManager.FullScreenHelper, screen, edge, 0)
        {
            _cloakMonitor = cloakMonitor;
            _shellManager = shellManager;

            InitializeComponent();
            DataContext = _shellManager;
            DesiredHeight = Application.Current.FindResource("TaskbarHeight") as double? ?? 0;
            AllowsTransparency = Application.Current.FindResource("AllowsTransparency") as bool? ?? false;
            SetFontSmoothing();
            SetupQuickLaunch();

            _explorerHelper.HideExplorerTaskbar = true;

            _cloakMonitor.PropertyChanged += CloakMonitor_PropertyChanged;
            Settings.Instance.PropertyChanged += Settings_PropertyChanged;

            // Layout rounding causes incorrect sizing on non-integer scales
            if(DpiHelper.DpiScale % 1 != 0) UseLayoutRounding = false;
        }

        protected override void OnSourceInitialized(object sender, EventArgs e)
        {
            base.OnSourceInitialized(sender, e);

            SetBlur(AllowsTransparency);
        }
        
        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            base.WndProc(hwnd, msg, wParam, lParam, ref handled);

            if ((msg == (int)NativeMethods.WM.SYSCOLORCHANGE || 
                    msg == (int)NativeMethods.WM.SETTINGCHANGE) && 
                Settings.Instance.Theme == "System")
            {
                handled = true;

                // If the color scheme changes, re-apply the current theme to get updated colors.
                ((App)Application.Current).ThemeManager.SetThemeFromSettings();
            }

            return IntPtr.Zero;
        }

        public override void SetPosition()
        {
            base.SetPosition();

            _shellManager.NotificationArea.SetTrayHostSizeData(new TrayHostSizeData
            {
                edge = (NativeMethods.ABEdge)AppBarEdge,
                rc = new NativeMethods.Rect
                {
                    Top = (int) (Top * DpiScale),
                    Left = (int) (Left * DpiScale),
                    Bottom = (int) ((Top + Height) * DpiScale),
                    Right = (int) ((Left + Width) * DpiScale)
                }
            });
        }

        private void SetFontSmoothing()
        {
            VisualTextRenderingMode = Settings.Instance.AllowFontSmoothing ? TextRenderingMode.Auto : TextRenderingMode.Aliased;
        }
        
        private void SetupQuickLaunch()
        {
            QuickLaunchToolbar.Folder?.Dispose();
            QuickLaunchToolbar.Folder = null;

            if (Settings.Instance.ShowQuickLaunch)
            {
                QuickLaunchToolbar.Folder = new ShellFolder(Environment.ExpandEnvironmentVariables(Utilities.Settings.Instance.QuickLaunchPath), IntPtr.Zero, true);
                QuickLaunchToolbar.Visibility = Visibility.Visible;
            }
            else
            {
                QuickLaunchToolbar.Visibility = Visibility.Collapsed;
            }
        }

        private void CloakMonitor_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            StartButton.SetStartMenuState(!_cloakMonitor.IsStartMenuCloaked);
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Theme")
            {
                bool newTransparency = Application.Current.FindResource("AllowsTransparency") as bool? ?? false;
                double newHeight = Application.Current.FindResource("TaskbarHeight") as double? ?? 0;

                if (AllowsTransparency != newTransparency)
                {
                    // Transparency cannot be changed on an open window.
                    _isReopening = true;
                    ((App)Application.Current).ReopenTaskbar();
                    return;
                }

                if (newHeight != DesiredHeight)
                {
                    DesiredHeight = newHeight;
                    SetScreenPosition();
                }
            }
            else if (e.PropertyName == "ShowQuickLaunch" || e.PropertyName == "QuickLaunchPath")
            {
                SetupQuickLaunch();
            }
            else if (e.PropertyName == "AllowFontSmoothing")
            {
                SetFontSmoothing();
            }
        }

        private void Taskbar_OnLocationChanged(object? sender, EventArgs e)
        {
            // primarily for win7/8, they will set up the appbar correctly but then put it in the wrong place
            double desiredTop = Screen.Bounds.Bottom / DpiScale - Height;

            if (Top != desiredTop) Top = desiredTop;
        }

        private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).ExitGracefully();
        }

        private void TaskManagerMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ShellHelper.StartTaskManager();
        }

        


        Shell32.Shell _shell = new Shell32.Shell();

        private void CascadeWindowsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ContextMenu.IsOpen = false;
            _shell.CascadeWindows();
            ShowUndoItem(sender);
        }

        private void StackWindowsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ContextMenu.IsOpen = false;
            _shell.TileVertically();
            ShowUndoItem(sender);
        }

        private void SideBySideWindowsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ContextMenu.IsOpen = false;
            _shell.TileHorizontally();
            ShowUndoItem(sender);
        }

        private void ShowDesktopMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ContextMenu.IsOpen = false;
            _shell.ToggleDesktop();
            ShowUndoItem(sender);
        }


        private void ShowUndoItem(object item)
        {
            UndoArrangeWindowsMenuItem.Header = "Undo " + ((System.Windows.Controls.MenuItem)item).Header.ToString();
            UndoArrangeWindowsMenuItem.Visibility = Visibility.Visible;
        }

        private void UndoArrangeWindowsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            _shell.UndoMinimizeALL();
            UndoArrangeWindowsMenuItem.Visibility = Visibility.Collapsed;
        }





        private void PropertiesMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            App app = (App)Application.Current;

            PropertiesWindow.Open(app.ThemeManager);
        }

        protected override void CustomClosing()
        {
            if (AllowClose)
            {
                if (!_isReopening) _explorerHelper.HideExplorerTaskbar = false;
                
                QuickLaunchToolbar.Folder?.Dispose();
                _cloakMonitor.PropertyChanged -= CloakMonitor_PropertyChanged;
                Settings.Instance.PropertyChanged -= Settings_PropertyChanged;
            }
        }
    }
}

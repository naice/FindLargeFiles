using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;

namespace FileSizeTree.Controls
{
    [TemplatePart(Name = PART_DragMoveElement, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PART_BtnClose, Type = typeof(Button))]
    [TemplatePart(Name = PART_BtnMaximize, Type = typeof(Button))]
    [TemplatePart(Name = PART_BtnMinimize, Type = typeof(Button))]
    [TemplatePart(Name = PART_BusyIndicator, Type = typeof(BusyIndicator))]
    [TemplatePart(Name = PART_BtnIcon, Type = typeof(Button))]
    public class ShellWindow : Window
    {
        public const string PART_DragMoveElement = "PART_DragMoveElement";
        public const string PART_BtnClose = "PART_BtnClose";
        public const string PART_BtnMaximize = "PART_BtnMaximize";
        public const string PART_BtnMinimize = "PART_BtnMinimize";
        public const string PART_BusyIndicator = "PART_BusyIndicator";
        public const string PART_BtnIcon = "PART_BtnIcon";

        /// <summary>
        /// Initializes the <see cref="ShellWindow"/> class.
        /// </summary>
        static ShellWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ShellWindow), new FrameworkPropertyMetadata(typeof(ShellWindow)));
        }

        public ShellWindow()
        {
            Loaded += ShellWindow_Loaded;
            DataContextChanged += ShellWindow_DataContextChanged;
        }


        bool _isLoaded = false;
        private void ShellWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            TryReportShellWindowLoaded();
        }


        private bool _isShellWindowLoaded = false;
        private void TryReportShellWindowLoaded()
        {
            if (_isShellWindowLoaded || !(DataContext is IShellWindowLoaded shellWindowLoaded))
                return;
            _isShellWindowLoaded = true;
            shellWindowLoaded.OnShellWindowLoaded();
        }

        private void ShellWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_isLoaded)
                TryReportShellWindowLoaded();
        }

        public Button MinimizeButton { get; private set; }
        public Button MaximizeButton { get; private set; }
        public Button CloseButton { get; private set; }
        public FrameworkElement DragMoveElement { get; private set; }
        public BusyIndicator BusyIndicator { get; private set; }
        public Button IconButton { get; private set; }

        public ICommand IconButtonCommand
        {
            get { return (ICommand)GetValue(IconButtonCommandProperty); }
            set { SetValue(IconButtonCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IconButtonCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IconButtonCommandProperty =
            DependencyProperty.Register(nameof(IconButtonCommand), typeof(ICommand), typeof(ShellWindow), new PropertyMetadata(null));

        public override void OnApplyTemplate()
        {
            MinimizeButton = GetTemplateChild(PART_BtnMinimize) as Button;
            MaximizeButton = GetTemplateChild(PART_BtnMaximize) as Button;
            CloseButton = GetTemplateChild(PART_BtnClose) as Button;
            DragMoveElement = GetTemplateChild(PART_DragMoveElement) as FrameworkElement;
            BusyIndicator = GetTemplateChild(PART_BusyIndicator) as BusyIndicator;
            IconButton = GetTemplateChild(PART_BtnIcon) as Button;

            if (CloseButton != null)
            {
                CloseButton.Click += CloseButton_Click;
            }

            if (MinimizeButton != null)
            {
                MinimizeButton.Click += MinimizeButton_Click;
            }

            if (MaximizeButton != null)
            {
                MaximizeButton.Click += MaximizeButton_Click;
            }

            if (DragMoveElement != null)
            {
                DragMoveElement.MouseLeftButtonDown += DragMoveElement_MouseLeftButtonDown;
            }

            WindowChrome.SetWindowChrome(this,
                new WindowChrome()
                {
                    CaptionHeight = 0,
                    ResizeBorderThickness = new Thickness(5, 5, 5, 5)
                });

            base.OnApplyTemplate();
        }

        private void DragMoveElement_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

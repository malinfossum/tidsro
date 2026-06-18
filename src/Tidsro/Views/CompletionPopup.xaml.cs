using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Tidsro.Services;
using Tidsro.ViewModels;

namespace Tidsro.Views;

public partial class CompletionPopup : Window
{
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private const int GWL_EXSTYLE = -20, WS_EX_NOACTIVATE = 0x08000000;

    private readonly PopupViewModel _vm;
    private IntPtr _previousForeground;

    public CompletionPopup(PopupViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.CloseRequested += (_, _) => Close();

        // remember who had focus so we can return it on dismiss
        _previousForeground = NativeFocus.GetForegroundWindow();

        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
        };

        // FocusForKeyboard clears NOACTIVATE to grab focus; re-arm it on blur so the
        // card returns to its calm, non-focus-stealing behaviour
        Deactivated += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            if (h != IntPtr.Zero)
                SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
        };

        // reveal actions on hover OR keyboard focus anywhere in the card
        MouseEnter += (_, _) => Actions.Opacity = 1;
        MouseLeave += (_, _) => { if (!IsKeyboardFocusWithin) Actions.Opacity = 0; };
        IsKeyboardFocusWithinChanged += (_, e) => { if ((bool)e.NewValue) Actions.Opacity = 1; else if (!IsMouseOver) Actions.Opacity = 0; };

        Loaded += (_, _) =>
        {
            UiaNotifier.Announce(this, _vm.AnnouncementText);
            if (!SystemParameters.ClientAreaAnimation)   // reduced motion -> no fade/slide
            {
                Opacity = 1;
                if (Root.RenderTransform is System.Windows.Media.TranslateTransform t0) t0.Y = 0;
                return;
            }
            Opacity = 0;
            var dur = (Duration)FindResource("DurationBase");
            BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, dur)
                { EasingFunction = new System.Windows.Media.Animation.CubicEase() });
            if (Root.RenderTransform is System.Windows.Media.TranslateTransform tt)
                tt.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(12, 0, dur)
                    { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
        };
        Closed += (_, _) => NativeFocus.Restore(_previousForeground);
    }

    /// <summary>Called by the global hotkey: pull this card into keyboard focus on demand.</summary>
    public void FocusForKeyboard()
    {
        // Activate() can't focus a WS_EX_NOACTIVATE window; clear the flag first, then
        // foreground + activate so WPF keyboard focus actually lands (Tab works on first press)
        var h = new WindowInteropHelper(this).Handle;
        SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) & ~WS_EX_NOACTIVATE);
        NativeFocus.SetForeground(h);
        Activate();   // now allowed (flag cleared) — ensures WPF activates the window
        Actions.Opacity = 1;
        Keyboard.Focus(DismissX);   // Deactivated re-arms NOACTIVATE on blur
    }
}

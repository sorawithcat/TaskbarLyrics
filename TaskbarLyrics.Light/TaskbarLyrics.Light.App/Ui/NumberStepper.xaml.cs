using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TaskbarLyrics.Light.App.Ui;

public partial class NumberStepper : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumberStepper),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumberStepper), new PropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumberStepper), new PropertyMetadata(100.0));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(double), typeof(NumberStepper), new PropertyMetadata(1.0));

    public static readonly DependencyProperty DecimalsProperty =
        DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(NumberStepper), new PropertyMetadata(0, OnValueChanged));

    public event EventHandler? ValueChanged;

    public NumberStepper()
    {
        InitializeComponent();
        DecreaseButton.Click += (_, _) => Adjust(-Step);
        IncreaseButton.Click += (_, _) => Adjust(Step);
        UpdateDisplay();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public int Decimals
    {
        get => (int)GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    private void Adjust(double delta)
    {
        Value = Math.Clamp(Value + delta, Minimum, Maximum);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumberStepper stepper)
        {
            stepper.UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        ValueBox.Text = Decimals <= 0
            ? Math.Round(Value).ToString(CultureInfo.InvariantCulture)
            : Math.Round(Value, Decimals).ToString($"F{Decimals}", CultureInfo.InvariantCulture);
    }
}

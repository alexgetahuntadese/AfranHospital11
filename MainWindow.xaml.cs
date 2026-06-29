using System.Globalization;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace AfranHospitalKiosk;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Random _animationRandom = new();
    private readonly QueueApiClient _apiClient = new();
    private int _ticketNumber = 105;
    private WizardStep _step = WizardStep.Language;
    private LanguageState _language = LanguageState.English;
    private string? _gender;

    public MainWindow()
    {
        InitializeComponent();
        StartClock();
        UpdateWizard();
    }

    private void StartClock()
    {
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockLabel.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        DateLabel.Text = now.ToString("dddd, dd MMM yyyy", CultureInfo.CurrentCulture);
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string language })
        {
            return;
        }

        _language = language == "Oromo" ? LanguageState.Oromo : LanguageState.Amharic;
        _step = WizardStep.Gender;
        UpdateWizard();
    }

    private async void GenderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string gender })
        {
            return;
        }

        // Disable buttons during processing
        SetButtonsEnabled(false);
        
        _gender = gender;
        _step = WizardStep.Printing;
        UpdateWizard();

        await Task.Delay(180);

        var ticket = await CreateLanTicketOrFallback(gender);
        TicketLabel.Text = ticket;
        var printed = TryPrintTicket(ticket);
        StatusLabel.Text = printed ? Text.Printed(ticket) : Text.PrintNotConfirmed(ticket);

        await Task.Delay(1800);

        ResetSelection();
        UpdateWizard();
        
        // Re-enable buttons for next registration
        SetButtonsEnabled(true);
    }

    private async Task<string> CreateLanTicketOrFallback(string gender)
    {
        try
        {
            var ticket = await _apiClient.CreateTicketAsync(gender, Text.LanguageName);
            if (!string.IsNullOrWhiteSpace(ticket))
            {
                return ticket;
            }
        }
        catch (Exception ex)
        {
            // Keep the kiosk useful if the LAN API is temporarily offline.
            System.Diagnostics.Debug.WriteLine($"API Error: {ex.Message}");
        }

        var fallback = CurrentTicket;
        _ticketNumber++;
        return fallback;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step == WizardStep.Gender)
        {
            ResetSelection();
            UpdateWizard();
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetSelection();
        UpdateWizard();
    }

    private void ResetSelection()
    {
        _language = LanguageState.English;
        _gender = null;
        _step = WizardStep.Language;
    }

    private void SetButtonsEnabled(bool enabled)
    {
        // Find all buttons in the language and gender panels and enable/disable them
        var languageButtons = FindVisualChildren<Button>(LanguagePanel);
        var genderButtons = FindVisualChildren<Button>(GenderPanel);
        
        foreach (var button in languageButtons.Concat(genderButtons))
        {
            button.IsEnabled = enabled;
        }
        
        // Also enable/disable the reset button
        var resetButton = this.FindName("NewButtonLabel") as TextBlock;
        if (resetButton != null)
        {
            var parentButton = resetButton.Parent as Button;
            if (parentButton != null)
            {
                parentButton.IsEnabled = enabled;
            }
        }
        
        // Enable/disable back button
        BackButton.IsEnabled = enabled && _step == WizardStep.Gender;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) return Enumerable.Empty<T>();
        
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
            
            if (child != null && child is T)
            {
                yield return (T)child;
            }
            
            foreach (T childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    private bool TryPrintTicket(string ticket)
    {
        try
        {
            var dialog = new PrintDialog
            {
                PrintQueue = LocalPrintServer.GetDefaultPrintQueue()
            };

            var ticketVisual = CreateTicketVisual(ticket);
            ticketVisual.Measure(new Size(320, 460));
            ticketVisual.Arrange(new Rect(0, 0, 320, 460));
            ticketVisual.UpdateLayout();

            dialog.PrintVisual(ticketVisual, $"Afran Hospital Registration {ticket}");
            return true;
        }
        catch (PrintSystemException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private FrameworkElement CreateTicketVisual(string ticket)
    {
        var now = DateTime.Now.ToString("dd MMM yyyy HH:mm", CultureInfo.CurrentCulture);

        return new Border
        {
            Width = 320,
            Height = 460,
            Background = Brushes.White,
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "AFRAN HOSPITAL",
                        FontSize = 24,
                        FontWeight = FontWeights.Black,
                        TextAlignment = TextAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = Text.TicketTitle,
                        FontSize = 16,
                        Margin = new Thickness(0, 6, 0, 30),
                        TextAlignment = TextAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = ticket,
                        FontSize = 52,
                        FontWeight = FontWeights.Black,
                        TextAlignment = TextAlignment.Center
                    },
                    new Separator { Margin = new Thickness(0, 26, 0, 22) },
                    new TextBlock { Text = $"{Text.TicketLanguageLabel}: {Text.LanguageName}", FontSize = 18, FontWeight = FontWeights.Bold },
                    new TextBlock { Text = $"{Text.TicketGenderLabel}: {Text.GenderName(_gender)}", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 0) },
                    new TextBlock { Text = Text.TicketCounter, FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 0) },
                    new TextBlock { Text = now, FontSize = 15, Margin = new Thickness(0, 26, 0, 0), TextAlignment = TextAlignment.Center },
                    new TextBlock
                    {
                        Text = Text.TicketFooter,
                        FontSize = 15,
                        Margin = new Thickness(0, 28, 0, 0),
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }

    private void UpdateWizard()
    {
        var isLanguage = _step == WizardStep.Language;
        var isGender = _step == WizardStep.Gender;
        var isPrinting = _step == WizardStep.Printing;

        LanguagePanel.Visibility = isLanguage ? Visibility.Visible : Visibility.Collapsed;
        GenderPanel.Visibility = isGender ? Visibility.Visible : Visibility.Collapsed;
        PrintingPanel.Visibility = isPrinting ? Visibility.Visible : Visibility.Collapsed;
        BackButton.IsEnabled = isGender;

        LanguageDot.Background = isLanguage ? FindBrush("GoldBrush") : FindBrush("ForestBrush");
        GenderDot.Background = isGender || isPrinting ? FindBrush("GoldBrush") : FindBrush("InkBrush");
        PrintDot.Background = isPrinting ? FindBrush("GoldBrush") : FindBrush("InkBrush");

        TicketLabel.Text = CurrentTicket;
        ApplyLanguageText();

        StatusLabel.Text = _step switch
        {
            WizardStep.Language => "Ready for registration.",
            WizardStep.Gender => Text.LanguageSelected,
            WizardStep.Printing => Text.Printing(CurrentTicket),
            _ => StatusLabel.Text
        };

        FadeActivePanel();
    }

    private void ApplyLanguageText()
    {
        SubtitleLabel.Text = Text.Subtitle;
        GenderStepLabel.Text = Text.StepTwo;
        GenderTitleLabel.Text = Text.ChooseGender;
        GenderHelperLabel.Text = Text.GenderHelp;
        FemaleCodeLabel.Text = Text.FemaleCode;
        FemaleLabel.Text = Text.Female;
        FemaleHintLabel.Text = Text.FemaleHint;
        MaleCodeLabel.Text = Text.MaleCode;
        MaleLabel.Text = Text.Male;
        MaleHintLabel.Text = Text.MaleHint;
        BackButtonLabel.Text = Text.Back;
        NewButtonLabel.Text = Text.New;
        PrintingTitleLabel.Text = Text.PrintingTitle;
        PrintingWowLabel.Text = Text.PrintingWow;
        PrintingHelperLabel.Text = Text.PrintingHelp;

        StepLabel.Text = "STEP 1 / 2";
        TitleLabel.Text = "ቋንቋ ይምረጡ / Afaan filadhu";
        HelperLabel.Text = "አማርኛ ወይም Afaan Oromoo ይምረጡ።";
    }

    private Brush FindBrush(string key)
    {
        return (Brush)FindResource(key);
    }

    private void FadeActivePanel()
    {
        var animation = new DoubleAnimation
        {
            From = 0.35,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        if (_step == WizardStep.Language)
        {
            LanguagePanel.BeginAnimation(OpacityProperty, animation);
        }
        else if (_step == WizardStep.Gender)
        {
            GenderPanel.BeginAnimation(OpacityProperty, animation);
        }
        else
        {
            PrintingPanel.BeginAnimation(OpacityProperty, animation);
        }
    }

    private void Floating3D_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var group = EnsureTransformGroup(element);
        var translate = GetTransform<TranslateTransform>(group);
        var skew = GetTransform<SkewTransform>(group);
        var delay = TimeSpan.FromMilliseconds(_animationRandom.Next(0, 700));

        var floatAnimation = new DoubleAnimation
        {
            From = -2,
            To = 3,
            BeginTime = delay,
            Duration = TimeSpan.FromSeconds(2.8 + _animationRandom.NextDouble()),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        var skewAnimation = new DoubleAnimation
        {
            From = -0.45,
            To = 0.45,
            BeginTime = delay,
            Duration = TimeSpan.FromSeconds(3.4 + _animationRandom.NextDouble()),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        translate.BeginAnimation(TranslateTransform.YProperty, floatAnimation);
        skew.BeginAnimation(SkewTransform.AngleXProperty, skewAnimation);
    }

    private void Component3D_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Animate3D(element, 1.035, -1.1, 0.7, -10, 170);
        }
    }

    private void Component3D_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Animate3D(element, 1, 0, 0, 0, 210);
        }
    }

    private void Component3D_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Animate3D(element, 0.985, 0.4, -0.35, 4, 90);
        }
    }

    private void Component3D_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Animate3D(element, 1.03, -0.8, 0.55, -8, 110);
        }
    }

    private void Animate3D(FrameworkElement element, double scale, double skewX, double skewY, double translateY, int milliseconds)
    {
        var group = EnsureTransformGroup(element);
        var scaleTransform = GetTransform<ScaleTransform>(group);
        var skewTransform = GetTransform<SkewTransform>(group);
        var translateTransform = GetTransform<TranslateTransform>(group);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        Animate(scaleTransform, ScaleTransform.ScaleXProperty, scale, milliseconds, ease);
        Animate(scaleTransform, ScaleTransform.ScaleYProperty, scale, milliseconds, ease);
        Animate(skewTransform, SkewTransform.AngleXProperty, skewX, milliseconds, ease);
        Animate(skewTransform, SkewTransform.AngleYProperty, skewY, milliseconds, ease);
        Animate(translateTransform, TranslateTransform.YProperty, translateY, milliseconds, ease);
    }

    private static void Animate(Animatable target, DependencyProperty property, double to, int milliseconds, IEasingFunction ease)
    {
        target.BeginAnimation(property, new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EasingFunction = ease
        });
    }

    private static TransformGroup EnsureTransformGroup(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup existing && !existing.IsFrozen)
        {
            EnsureTransform<ScaleTransform>(existing);
            EnsureTransform<SkewTransform>(existing);
            EnsureTransform<TranslateTransform>(existing);
            return existing;
        }

        var group = new TransformGroup();
        if (element.RenderTransform is TransformGroup frozenGroup)
        {
            var scale = frozenGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
            var skew = frozenGroup.Children.OfType<SkewTransform>().FirstOrDefault();
            var translate = frozenGroup.Children.OfType<TranslateTransform>().FirstOrDefault();

            group.Children.Add(new ScaleTransform(scale?.ScaleX ?? 1, scale?.ScaleY ?? 1));
            group.Children.Add(new SkewTransform(skew?.AngleX ?? 0, skew?.AngleY ?? 0));
            group.Children.Add(new TranslateTransform(translate?.X ?? 0, translate?.Y ?? 0));
        }
        else
        {
            group.Children.Add(new ScaleTransform(1, 1));
            group.Children.Add(new SkewTransform());
            group.Children.Add(new TranslateTransform());
        }

        element.RenderTransform = group;
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        return group;
    }

    private static T GetTransform<T>(TransformGroup group)
        where T : Transform
    {
        return group.Children.OfType<T>().First();
    }

    private static void EnsureTransform<T>(TransformGroup group)
        where T : Transform, new()
    {
        if (!group.Children.OfType<T>().Any())
        {
            group.Children.Add(new T());
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _apiClient.DisposeAsync();
        base.OnClosed(e);
    }

    private string CurrentTicket => $"REG-{_ticketNumber:000}";

    private LanguageText Text => _language switch
    {
        LanguageState.Amharic => LanguageText.Amharic,
        LanguageState.Oromo => LanguageText.Oromo,
        _ => LanguageText.English
    };

    private enum WizardStep
    {
        Language,
        Gender,
        Printing
    }

    private enum LanguageState
    {
        English,
        Amharic,
        Oromo
    }

    private sealed record LanguageText(
        string Subtitle,
        string StepTwo,
        string ChooseGender,
        string GenderHelp,
        string FemaleCode,
        string Female,
        string FemaleHint,
        string MaleCode,
        string Male,
        string MaleHint,
        string Back,
        string New,
        string LanguageName,
        string LanguageSelected,
        string PrintingTitle,
        string PrintingWow,
        string PrintingHelp,
        string TicketTitle,
        string TicketLanguageLabel,
        string TicketGenderLabel,
        string TicketCounter,
        string TicketFooter)
    {
        public string Printing(string ticket) => this == English
            ? $"Printing {ticket}..."
            : this == Oromo
                ? $"{ticket} maxxanfamaa jira..."
                : $"{ticket} በመታተም ላይ...";

        public string Printed(string ticket) => this == English
            ? $"Printed {ticket}. Go to Registration Counter 2."
            : this == Oromo
                ? $"{ticket} maxxanfame. Gara Galmee Kaawuntara 2 deemaa."
                : $"{ticket} ታትሟል። ወደ መመዝገቢያ ቆጣሪ 2 ይሂዱ።";

        public string PrintNotConfirmed(string ticket) => this == English
            ? $"{ticket} is ready. Printer was not confirmed."
            : this == Oromo
                ? $"{ticket} qophaa'e. Maxxansi hin mirkanoofne."
                : $"{ticket} ዝግጁ ነው። ፕሪንተሩ አልተረጋገጠም።";

        public string GenderName(string? gender) => gender == "Female" ? Female : Male;

        public static readonly LanguageText English = new(
            "Registration kiosk",
            "STEP 2 / 2",
            "Choose gender",
            "Ticket prints automatically after selection.",
            "F",
            "Female",
            "Print female registration ticket",
            "M",
            "Male",
            "Print male registration ticket",
            "Back",
            "New",
            "English",
            "Language selected.",
            "Thank you",
            "Your registration ticket is ready.",
            "Please take your ticket. Our team will call your number shortly.",
            "Registration Ticket",
            "Language",
            "Gender",
            "Counter: Registration 2",
            "Please keep this ticket visible.");

        public static readonly LanguageText Amharic = new(
            "የመመዝገቢያ ኪዮስክ",
            "ደረጃ 2 / 2",
            "ጾታ ይምረጡ",
            "ከመረጡ በኋላ ቲኬቱ ወዲያው ይታተማል።",
            "ሴ",
            "ሴት",
            "የሴት መመዝገቢያ ቲኬት አትም",
            "ወ",
            "ወንድ",
            "የወንድ መመዝገቢያ ቲኬት አትም",
            "ተመለስ",
            "አዲስ",
            "አማርኛ",
            "ቋንቋ ተመርጧል።",
            "እናመሰግናለን",
            "የመመዝገቢያ ቲኬትዎ ዝግጁ ነው።",
            "እባክዎ ቲኬቱን ይውሰዱ። ቡድናችን ቁጥርዎን በቅርቡ ይጠራል።",
            "የመመዝገቢያ ቲኬት",
            "ቋንቋ",
            "ጾታ",
            "ቆጣሪ፦ መመዝገቢያ 2",
            "እባክዎ ይህን ቲኬት በግልጽ ያስቀምጡ።");

        public static readonly LanguageText Oromo = new(
            "Kiyooskii galmee",
            "TARKAANFII 2 / 2",
            "Saala filadhu",
            "Erga filattee booda tikeetiin ofumaan maxxanfama.",
            "D",
            "Dubartii",
            "Tikeetii galmee dubartii maxxansiisi",
            "Dh",
            "Dhiira",
            "Tikeetii galmee dhiiraa maxxansiisi",
            "Duuba",
            "Haaraa",
            "Oromo",
            "Afaan filatameera.",
            "Galatoomi",
            "Tikeetiin galmee kee qophaa'eera.",
            "Maaloo tikeetii kee fudhadhu. Gareen keenya lakkoofsa kee dhi soon ni waama.",
            "Tikeetii Galmee",
            "Afaan",
            "Saala",
            "Kaawuntara: Galmee 2",
            "Maaloo tikeetii kana mul'isuuf qabadhu.");
    }
}

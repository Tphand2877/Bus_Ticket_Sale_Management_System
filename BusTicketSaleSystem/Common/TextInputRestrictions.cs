using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BusTicketSaleSystem.Common
{
    /// <summary>
    /// Attached behaviours that stop a <see cref="TextBox"/> accepting characters it can never
    /// use. Phone and seat-count fields previously accepted letters and only complained on submit.
    /// <para>
    /// Implemented as an attached property rather than per-view code-behind so the rule lives in
    /// one place and is declared where it applies: <c>common:TextInputRestrictions.DigitsOnly="True"</c>.
    /// </para>
    /// </summary>
    public static class TextInputRestrictions
    {
        /// <summary>Characters allowed in a phone number alongside digits.</summary>
        private const string PhonePunctuation = " .-()+";

        public static readonly DependencyProperty DigitsOnlyProperty =
            DependencyProperty.RegisterAttached(
                "DigitsOnly",
                typeof(bool),
                typeof(TextInputRestrictions),
                new PropertyMetadata(false, OnDigitsOnlyChanged));

        public static void SetDigitsOnly(DependencyObject element, bool value)
            => element.SetValue(DigitsOnlyProperty, value);

        public static bool GetDigitsOnly(DependencyObject element)
            => (bool)element.GetValue(DigitsOnlyProperty);

        public static readonly DependencyProperty PhoneNumberProperty =
            DependencyProperty.RegisterAttached(
                "PhoneNumber",
                typeof(bool),
                typeof(TextInputRestrictions),
                new PropertyMetadata(false, OnPhoneNumberChanged));

        public static void SetPhoneNumber(DependencyObject element, bool value)
            => element.SetValue(PhoneNumberProperty, value);

        public static bool GetPhoneNumber(DependencyObject element)
            => (bool)element.GetValue(PhoneNumberProperty);

        private static void OnDigitsOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => Attach(d, (bool)e.NewValue, char.IsDigit);

        private static void OnPhoneNumberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => Attach(d, (bool)e.NewValue, c => char.IsDigit(c) || PhonePunctuation.Contains(c));

        private static void Attach(DependencyObject d, bool enabled, Func<char, bool> isAllowed)
        {
            if (d is not TextBox box) return;

            // Detach first so toggling the property off actually removes the restriction.
            box.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(box, OnPaste);

            if (!enabled) return;

            box.SetValue(FilterProperty, isAllowed);
            box.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(box, OnPaste);
        }

        private static readonly DependencyProperty FilterProperty =
            DependencyProperty.RegisterAttached(
                "Filter",
                typeof(Func<char, bool>),
                typeof(TextInputRestrictions),
                new PropertyMetadata(null));

        private static Func<char, bool> FilterFor(DependencyObject d)
            => d.GetValue(FilterProperty) as Func<char, bool> ?? char.IsDigit;

        private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox box) return;
            var isAllowed = FilterFor(box);
            e.Handled = !e.Text.All(isAllowed);
        }

        private static void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox box) return;
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                e.CancelCommand();
                return;
            }

            var pasted = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
            var isAllowed = FilterFor(box);
            if (!pasted.All(isAllowed))
            {
                e.CancelCommand();
            }
        }
    }
}

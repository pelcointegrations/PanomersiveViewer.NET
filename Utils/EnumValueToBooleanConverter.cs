using System;
using System.Windows.Data;

namespace PanomersiveViewerNET.Utils
{
    /// <summary>
    /// Converts enum values to boolean.
    /// </summary>
    public class EnumValueToBooleanConverter : IValueConverter
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="EnumValueToBooleanConverter"/> is inverted.
        /// </summary>
        public bool Inverted { get; set; }

        /// <summary>
        /// The enum value used for True.
        /// </summary>
        public object TrueEnum { get; set; }

        /// <summary>
        /// The enum value used for False.
        /// </summary>
        public object FalseEnum { get; set; }

        #endregion

        public EnumValueToBooleanConverter()
        {
            FalseEnum = Binding.DoNothing;
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // Use parameter as the first choice.
            var compareTo = parameter ?? TrueEnum;

            // Returns inverse of the value if Inverted is true.
            if (Inverted)
                return value != null && !value.Equals(compareTo);

            return value != null && value.Equals(compareTo);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // Use parameter as the first choice.
            var trueEnum = parameter ?? TrueEnum;

            return value != null && value.Equals(true) ? trueEnum : FalseEnum;
        }
    }
}

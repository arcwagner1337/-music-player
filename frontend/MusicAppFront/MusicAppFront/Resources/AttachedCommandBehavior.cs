using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace MusicAppFront.Resources
{
    public static class ClickHelper
    {
        public static readonly DependencyProperty HandledProperty =
            DependencyProperty.RegisterAttached("Handled", typeof(bool), typeof(ClickHelper),
            new PropertyMetadata(false, OnHandledChanged));

        public static void SetHandled(UIElement element, bool value) => element.SetValue(HandledProperty, value);
        public static bool GetHandled(UIElement element) => (bool)element.GetValue(HandledProperty);

        private static void OnHandledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element && (bool)e.NewValue)
            {

                element.AddHandler(
                    ButtonBase.ClickEvent,
                    new RoutedEventHandler(OnElementClick),
                    handledEventsToo: true 
                );
            }
        }

        private static void OnElementClick(object sender, RoutedEventArgs e)
        {

            e.Handled = true;
        }
    }
}

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
                // Используем специальную перегрузку AddHandler, которая умеет перехватывать 
                // даже те события, которые сама кнопка помечает как Handled.
                // Мы ловим именно событие ClickEvent у ButtonBase.
                element.AddHandler(
                    ButtonBase.ClickEvent,
                    new RoutedEventHandler(OnElementClick),
                    handledEventsToo: true // Вот этот флаг — магия WPF
                );
            }
        }

        private static void OnElementClick(object sender, RoutedEventArgs e)
        {
            // Говорим WPF: "Всё, дальше этого элемента событие клика не летит!"
            // Кнопка сама уже отработала, IsChecked поменялся, попап открылся.
            // А родитель-трек теперь этот клик тупо не услышит.
            e.Handled = true;
        }
    }
}

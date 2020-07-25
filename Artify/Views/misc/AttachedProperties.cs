using System.Windows;

namespace Artify.Views.misc
{
    public static class AttachedProperties
    {
        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached("Placeholder",
                                                                                                    typeof(string),
                                                                                                    typeof(AttachedProperties));
        public static readonly DependencyProperty IsEmptyProperty = DependencyProperty.RegisterAttached("IsEmpty",
                                                                                                        typeof(bool),
                                                                                                        typeof(AttachedProperties), new PropertyMetadata(false));

        public static string GetPlaceholder(UIElement element)
            => element?.GetValue(PlaceholderProperty) as string;

        public static void SetPlaceholder(UIElement element, string value)
            => element?.SetValue(PlaceholderProperty, value);

        public static bool GetIsEmpty(UIElement element)
            => (bool)(element?.GetValue(IsEmptyProperty) ?? true);

        public static void SetIsEmpty(UIElement element, bool value)
            => element?.SetValue(IsEmptyProperty, value);
    }
}
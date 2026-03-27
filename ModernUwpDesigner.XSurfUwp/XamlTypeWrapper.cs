using System;
using Windows.UI.Xaml.Markup;

namespace XSurfUwp
{
    internal partial class XamlTypeWrapper(IXamlType type, string fullname) : IXamlType, IXamlType2
    {
        private readonly IXamlType2 _type2 = type as IXamlType2;

        public IXamlType BaseType => type.BaseType;

        public IXamlMember ContentProperty => type.ContentProperty;

        public string FullName => fullname;

        public bool IsArray => type.IsArray;

        public bool IsBindable => type.IsBindable;

        public bool IsCollection => type.IsCollection;

        public bool IsConstructible => type.IsConstructible;

        public bool IsDictionary => type.IsDictionary;

        public bool IsMarkupExtension => type.IsMarkupExtension;

        public IXamlType ItemType => type.ItemType;

        public IXamlType KeyType => type.KeyType;

        public Type UnderlyingType => type.UnderlyingType;

        public IXamlType BoxedType => _type2?.BoxedType;

        public object ActivateInstance()
        {
            return type.ActivateInstance();
        }

        public void AddToMap(object instance, object key, object value)
        {
            type.AddToMap(instance, key, value);
        }

        public void AddToVector(object instance, object value)
        {
            type.AddToVector(instance, value);
        }

        public object CreateFromString(string value)
        {
            return type.CreateFromString(value);
        }

        public IXamlMember GetMember(string name)
        {
            return type.GetMember(name);
        }

        public void RunInitializer()
        {
            type.RunInitializer();
        }
    }
}

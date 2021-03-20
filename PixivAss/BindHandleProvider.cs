using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace PixivAss
{
    /*
     * 一个物体上的属性更新，所有属性都会被重新获取
     * 因此需要将不同属性绑定到不同object上，使用一个基类创建用于绑定的object(BindHandle)
     * 但是C#不能多继承，而接口不能实现方法
     * 因此在自接口继承的实现类中实现方法，将同名静态方法添加到静态扩展类，从而让继承了接口的类可以间接调用实现类
     */
    public class BindHandle<T> : INotifyPropertyChanged
    {
        public T Content { get { return (T)parent.GetType().GetProperty(prop_name).GetValue(parent); } }
        private object parent;
        private string prop_name;
        public event PropertyChangedEventHandler PropertyChanged;
        public BindHandle(object _parent, string _prop_name)
        {
            parent = _parent;
            prop_name = _prop_name;
        }
        public void NotifyChange()
        {
            PropertyChanged(this, new PropertyChangedEventArgs("Content"));
        }
    }
    public interface IBindHandleProvider
    {
        BindHandleProvider provider { get; set; }
    }
    public class BindHandleProvider
    {
        private Dictionary<string, object> handle_map = new Dictionary<string, object>();
        public BindHandle<T> GetBindHandle<T>(object obj, string property_name)
        {
            if (!handle_map.ContainsKey(property_name))
                handle_map.Add(property_name, new BindHandle<T>(obj, property_name));
            return (BindHandle<T>)handle_map[property_name];
        }
        public void NotifyChange<T>(string property_name)
        {
            if (handle_map.ContainsKey(property_name))
                ((BindHandle<T>)handle_map[property_name]).NotifyChange();
        }
    }
    public static class BindHandleProviderExtension
    {
        public static MainWindow main_window;
        public static void NotifyChange<T>(this IBindHandleProvider obj, string property_name)
        {
            obj.provider.NotifyChange<T>(property_name);
            main_window.Update();//由于蜜汁原因，在x64下频繁绘制足够大的GIF时，其它控件不会根据databinding自动刷新，所以需要让BindHandle手动调用MainWindow.Update()
        }

        public static void NotifyChangeRange<T>(this IBindHandleProvider obj, List<string> property_names)
        {
            foreach (var property_name in property_names)
                obj.provider.NotifyChange<T>(property_name);
        }
        public static void NotifyChangeEx<T>(this IBindHandleProvider obj, [CallerMemberName]string property_name = "")
        {
            obj.provider.NotifyChange<T>(property_name);
        }
        public static BindHandle<T> GetBindHandle<T>(this IBindHandleProvider obj, string property_name)
        {
            return obj.provider.GetBindHandle<T>(obj, property_name);
        }
    }
}

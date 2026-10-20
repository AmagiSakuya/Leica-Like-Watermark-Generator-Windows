using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace leica_watermark_generator.Models
{
    /// <summary>
    /// 轻量级的 INotifyPropertyChanged 基类，供绑定用
    /// </summary>
    public abstract class Observable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Raise([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}

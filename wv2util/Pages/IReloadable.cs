using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wv2util
{
    public interface IReloadable
    {
        void Reload();
        bool Reloading { get; }

        event EventHandler ReloadingChanged;
    }
}

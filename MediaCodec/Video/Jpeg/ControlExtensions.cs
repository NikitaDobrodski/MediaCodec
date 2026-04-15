using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MediaCodec;

internal static class ControlExtensions
{
    /// <summary>
    /// Вызвать действие, если элемент управления не удален
    /// </summary>
    /// <param name="control">Элемент управления</param>
    /// <param name="action">Действие</param>
    internal static void InvokeIfRequired(this Control control, Action action)
    {
        if (control.IsDisposed) return;

        if (control.InvokeRequired)
            control.Invoke(action);
        else
            action();
    }
}
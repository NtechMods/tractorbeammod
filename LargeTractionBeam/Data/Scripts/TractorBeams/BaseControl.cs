using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;

using Sandbox.ModAPI;
using VRage.ObjectBuilders;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace LSE.Control
{
    public class BaseControl<T>
    {
        public SerializableDefinitionId Definition;
        public string InternalName;
        public string Title;

        public BaseControl(
            IMyTerminalBlock block,
            string internalName,
            string title)
        {
            Definition = block.BlockDefinition;
            InternalName = internalName + Definition.SubtypeId;
            Title = title;
        }

        public void CreateUI()
        {
            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            var control = controls.Find((x) => x.Id.ToString() == InternalName);
            if (control == null)
            {
                OnCreateUI();
            }
        }

        public virtual void OnCreateUI()
        {
        }

        public virtual bool Enabled(IMyTerminalBlock block)
        {
            return true;
        }

        public virtual bool ShowControl(IMyTerminalBlock block)
        {
            return block.BlockDefinition.TypeId == Definition.TypeId &&
                   block.BlockDefinition.SubtypeId == Definition.SubtypeId;
        }
    }
}

/*
Copyright © 2016 Leto
This work is free. You can redistribute it and/or modify it under the
terms of the Do What The Fuck You Want To Public License, Version 2,
as published by Sam Hocevar. See http://www.wtfpl.net/ for more details.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Collections;

using Sandbox.ModAPI;
using VRage.ObjectBuilders;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;

namespace LSE.Control
{
    public class Slider<T> : BaseControl<T>
    {
        public float Min;
        public float Max;
        public float Standard;
        public bool Log;

        public Slider(
            IMyTerminalBlock block,
            string internalName,
            string title,
            float min = 0.0f,
            float max = 100.0f,
            float standard = 10.0f,
            bool log = false)            
            : base(block, internalName, title)
        {
            Log = log;
            Min = min;
            Max = max;
            Standard = standard;
            CreateUI();
        }

        public override void OnCreateUI()
        {
            var slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(InternalName);
            slider.Visible = ShowControl;
            if (Log)
            {
                slider.SetLogLimits(Min, Max);
            }
            else
            {
                slider.SetLimits(Min, Max);
            }
            slider.Getter = Getter;
            slider.Setter = Setter;
            slider.Enabled = Enabled;
            slider.Writer = Writer;
            slider.Title = VRage.Utils.MyStringId.GetOrCompute(Title);
            MyAPIGateway.TerminalControls.AddControl<T>(slider);
        }

        public virtual void Writer(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Clear();
            builder.Append(Getter(block).ToString());
        }

        public virtual float Getter(IMyTerminalBlock block)
        {
            float value = Standard;
            if (MyAPIGateway.Utilities.GetVariable<float>(block.EntityId.ToString() + InternalName, out value))
            {
                return value;
            }
            return Standard;
        }
        
        public virtual void Setter(IMyTerminalBlock block, float value)
        {
            value = Math.Max(Math.Min(value, Max), Min);
            MyAPIGateway.Utilities.SetVariable<float>(block.EntityId.ToString() + InternalName, value);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Collections;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.ModAPI;
using VRage.Game.Components;
using VRageMath;
using Sandbox.Engine.Multiplayer;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;


namespace LSE
{
	
    public class TractorUI<T>
    {
        public MaxSlider<T> MaxSlider;
        public MinSlider<T> MinSlider;
        public StrengthSlider<T> StrengthSlider;

        public bool Initialized_Attractor = false;
        public void CreateUI(IMyTerminalBlock block)
        {
            if (Initialized_Attractor) { return; }
            Initialized_Attractor = true;

            MinSlider = new MinSlider<T>(this, 
                block,
              "MinSlider",
              "Minimum Distance",
              3, 200, 60);
            MaxSlider = new MaxSlider<T>(this,
                block,
              "MaxSlider",
              "Maximum Distance",
              3, 200, 80);
            StrengthSlider = new StrengthSlider<T>(this,
                block,
              "StrengthSlider",
              "Strength",
              2000, 990000, 80000, true);

            new SliderAction<T>(block, "IncMax", "Increase maximum distance", "", MaxSlider, 1.0f);
            new SliderAction<T>(block, "DecMax", "Decrease maximum distance", "", MaxSlider, -1.0f);
            new SliderAction<T>(block, "IncMin", "Increase minimum distance", "", MinSlider, 1.0f);
            new SliderAction<T>(block, "DecMin", "Decrease minimum distance", "", MinSlider, -1.0f);
            new SliderAction<T>(block, "IncStr", "Increase strength", "", StrengthSlider, 1.1f);
            new SliderAction<T>(block, "DecStr", "Decrease strength", "", StrengthSlider, 0.9f);

        }

        public void Sync(IMyTerminalBlock block)
        {
            var msg = new LSE.TractorNetwork.MessageConfig();
            msg.EntityId = block.EntityId;
            msg.Min = MinSlider.Getter(block);
            msg.Max = MaxSlider.Getter(block);
            msg.Strength = StrengthSlider.Getter(block);
            TractorNetwork.MessageUtils.SendMessageToAll(msg);
        }
    }

    public class MaxSlider<T> : LSE.Control.Slider<T>
    {
        private TractorUI<T> m_ui;
        public MaxSlider(
            TractorUI<T> ui,
            IMyTerminalBlock block,
            string internalName,
            string title,
            float min = 0.0f,
            float max = 100.0f,
            float standard = 10.0f)
                : base(block, internalName, title, min, max, standard)
        {
            m_ui = ui;
            CreateUI();
        }

        public override void Writer(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Clear();
            builder.Append(Getter(block).ToString() + " m");
        }

        public override void Setter(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            var minSlider = controls.Find((x) => x.Id == "MinSlider" + Definition.SubtypeId);
            if (minSlider != null && m_ui.MinSlider != null)
            {
                var minValue = m_ui.MinSlider.Getter(block);
                m_ui.MinSlider.SetterNoCheck(block, Math.Min(minValue, value));
                minSlider.UpdateVisual();
                m_ui.Sync(block);
            }
        }

        public void SetterNoCheck(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
        }
    }


    public class MinSlider<T> : LSE.Control.Slider<T>
    {
        private TractorUI<T> m_ui;
        public MinSlider(
            TractorUI<T> ui,
            IMyTerminalBlock block,
            string internalName,
            string title,
            float min = 0.0f,
            float max = 100.0f,
            float standard = 10.0f)
                : base(block, internalName, title, min, max, standard)
        {
            m_ui = ui;
            CreateUI();
        }

        public override void Writer(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Clear();
            builder.Append(Getter(block).ToString() + " m");
        }

        public void SetterNoCheck(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
        }

        public override void Setter(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            var maxSlider = controls.Find((x) => x.Id == "MaxSlider" + Definition.SubtypeId);
            if (maxSlider != null && m_ui.MaxSlider != null)
            {
                var maxValue = m_ui.MaxSlider.Getter(block);
                m_ui.MaxSlider.SetterNoCheck(block, Math.Max(maxValue, value));
                maxSlider.UpdateVisual();
                m_ui.Sync(block);
            }
        }
    }


    public class StrengthSlider<T> : LSE.Control.Slider<T>
    {
        private TractorUI<T> m_ui;
        public StrengthSlider(
            TractorUI<T> ui,
            IMyTerminalBlock block,
            string internalName,
            string title,
            float min = 1.0f,
            float max = 10000.0f,
            float standard = 2000.0f,
            bool log = false)
                : base(block, internalName, title, min, max, standard, log)
        {
            m_ui = ui;
            CreateUI();
        }

        public override void Writer(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Clear();
            builder.Append(Getter(block).ToString() + " N");
        }

        public override void Setter(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            m_ui.Sync(block);
        }

        public void SetterNoCheck(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
        }

    }

    public class SliderAction<T> : Control.ControlAction<T>
    {
        LSE.Control.Slider<T> Slider;
        float IncPerAction;

        public SliderAction(
            IMyTerminalBlock block,
            string internalName,
            string name,
            string icon,
            LSE.Control.Slider<T> slider,
            float incPerAction)
            : base(block, internalName, name, icon)
        {
            Slider = slider;
            IncPerAction = incPerAction;
        }

        public override void OnAction(IMyTerminalBlock block)
        {
            if (Slider.Log)
            {
                Slider.Setter(block, Slider.Getter(block) * IncPerAction);
            }
            else
            {
                Slider.Setter(block, Slider.Getter(block) + IncPerAction);
            }
        }
    }
}

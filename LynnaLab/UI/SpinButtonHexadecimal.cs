﻿using System;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class SpinButtonHexadecimal : Gtk.SpinButton
    {
        public SpinButtonHexadecimal() : base(0,255,1)
        {
            this.Numeric = false;
        }

        protected override int OnOutput() {
            this.Numeric = false;
            if (ValueAsInt < 0)
                Text = "-$" + Math.Abs(ValueAsInt).ToString("X" + Digits);
            else
                Text = "$" + ValueAsInt.ToString("X" + Digits);
            return 1;
        }
        protected override int OnInput(out double value) {
            string text = Text.Trim();
            bool success = false;
            value = Value;
            // Try a decimal
            try {
                value = Convert.ToInt32(text);
                success = true;
            }
            catch (Exception e) {
            }
            // Try a hex number
            try {
                if (text.Length > 0 && text[0] == '$') {
                    value = Convert.ToInt32(text.Substring(1),16);
                    success = true;
                }
            }
            catch (Exception) {
            }
            // Try a negative hex number
            try {
                if (text.Length > 1 && text[0] == '-' && text[1] == '$') {
                    value = -Convert.ToInt32(text.Substring(2),16);
                    success = true;
                }
            }
            catch (Exception) {
            }
            if (!success)
                value = Value;
            return 1;
        }
    }
}


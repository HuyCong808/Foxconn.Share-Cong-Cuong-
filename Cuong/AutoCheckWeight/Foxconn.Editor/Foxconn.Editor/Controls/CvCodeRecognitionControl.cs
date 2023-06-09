﻿using Foxconn.Editor.Enums;
using Foxconn.Editor.OpenCV;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Foxconn.Editor.Controls
{
    public class CvCodeRecognitionControl : UserControl
    {
        #region Binding Property
        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register("Mode", typeof(CodeMode), typeof(CvCodeRecognitionControl));
        public static readonly DependencyProperty FormatProperty = DependencyProperty.Register("Format", typeof(CodeFormat), typeof(CvCodeRecognitionControl));
        public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register("Prefix", typeof(string), typeof(CvCodeRecognitionControl));
        public static readonly DependencyProperty LengthProperty = DependencyProperty.Register("Length", typeof(int), typeof(CvCodeRecognitionControl));

        public CodeMode Mode
        {
            get => (CodeMode)GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public CodeFormat Format
        {
            get => (CodeFormat)GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }

        public string Prefix
        {
            get => (string)GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public int Length
        {
            get => (int)GetValue(LengthProperty);
            set => SetValue(LengthProperty, value);
        }

        // Declare event
        public event PropertyChangedEventHandler PropertyChanged;
        // NotifyPropertyChanged method to update property value in binding
        public void NotifyPropertyChanged(string info = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
        #endregion

        public CvCodeRecognitionControl()
        {

        }
        public void SetParameters(CvCodeRecognition param)
        {
            string[] paths = new string[] { "Mode", "Format", "Prefix", "Length" };
            DependencyProperty[] properties = new DependencyProperty[] { ModeProperty, FormatProperty, PrefixProperty, LengthProperty };
            for (int i = 0; i < paths.Length; i++)
            {
                Binding binding = new Binding(paths[i])
                {
                    Source = param,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                SetBinding(properties[i], binding);
            }
            NotifyPropertyChanged();
        }


    }
}

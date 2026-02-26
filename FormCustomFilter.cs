#region License
// Advanced DataGridView
//
// Copyright (c), 2014 Davide Gironi <davide.gironi@gmail.com>
// Original work Copyright (c), 2013 Zuby <zuby@me.com>
//
// Please refer to LICENSE file for licensing information.
#endregion

using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace Zuby.ADGV
{
    public partial class FormCustomFilter : Form
    {
        private bool _isDarkMode = false;

        #region class properties

        private enum FilterType
        {
            Unknown,
            DateTime,
            TimeSpan,
            String,
            Float,
            Integer
        }

        private FilterType _filterType = FilterType.Unknown;
        private Control _valControl1 = null;
        private Control _valControl2 = null;

        private bool _filterDateAndTimeEnabled = true;

        private string _filterString = null;
        private string _filterStringDescription = null;

        #endregion


        #region constructors

        /// <summary>
        /// Main constructor
        /// </summary>
        /// <param name="dataType"></param>
        /// <param name="filterDateAndTimeEnabled"></param>
        /// <param name="initialFilterType"></param>
        public FormCustomFilter(Type dataType, bool filterDateAndTimeEnabled, string initialFilterType = null)
            : base()
        {
            //initialize components
            InitializeComponent();

            //set component translations
            this.Text = AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVFormTitle.ToString()];
            this.label_columnName.Text = AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLabelColumnNameText.ToString()];
            this.label_and.Text = AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLabelAnd.ToString()];
            this.button_ok.Text = AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVButtonOk.ToString()];
            this.button_cancel.Text = AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVButtonCancel.ToString()];

            if (dataType == typeof(DateTime))
                _filterType = FilterType.DateTime;
            else if (dataType == typeof(TimeSpan))
                _filterType = FilterType.TimeSpan;
            else if (dataType == typeof(Int32) || dataType == typeof(Int64) || dataType == typeof(Int16) ||
                    dataType == typeof(UInt32) || dataType == typeof(UInt64) || dataType == typeof(UInt16) ||
                    dataType == typeof(Byte) || dataType == typeof(SByte))
                _filterType = FilterType.Integer;
            else if (dataType == typeof(Single) || dataType == typeof(Double) || dataType == typeof(Decimal))
                _filterType = FilterType.Float;
            else if (dataType == typeof(String))
                _filterType = FilterType.String;
            else
                _filterType = FilterType.Unknown;

            _filterDateAndTimeEnabled = filterDateAndTimeEnabled;

            // Setup Value Controls
            switch (_filterType)
            {
                case FilterType.DateTime:
                    _valControl1 = new DateTimePicker();
                    _valControl2 = new DateTimePicker();
                    DateTimeFormatInfo dt = Thread.CurrentThread.CurrentCulture.DateTimeFormat;
                    foreach(var c in new[] { _valControl1, _valControl2 }) {
                        var d = c as DateTimePicker;
                        if (_filterDateAndTimeEnabled) {
                            d.CustomFormat = dt.ShortDatePattern + " " + "HH:mm";
                            d.Format = DateTimePickerFormat.Custom;
                        } else {
                            d.Format = DateTimePickerFormat.Short;
                        }
                    }

                    comboBox_filterType.Items.AddRange(new string[] {
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEquals.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotEqual.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEarlierThan.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEarlierThanOrEqualTo.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLaterThan.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLaterThanOrEqualTo.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVBetween.ToString()]
                    });
                    break;

                case FilterType.TimeSpan:
                    _valControl1 = new TextBox();
                    _valControl2 = new TextBox();
                    comboBox_filterType.Items.AddRange(new string[] {
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVContains.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotContain.ToString()]
                    });
                    break;

                case FilterType.Integer:
                case FilterType.Float:
                    _valControl1 = new TextBox();
                    _valControl2 = new TextBox();
                    _valControl1.Tag = true; _valControl2.Tag = true;
                    _valControl1.TextChanged += valControl_TextChanged;
                    _valControl2.TextChanged += valControl_TextChanged;
                    comboBox_filterType.Items.AddRange(new string[] {
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEquals.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotEqual.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVGreaterThan.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVGreaterThanOrEqualTo.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLessThan.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLessThanOrEqualTo.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVBetween.ToString()]
                    });
                    break;

                default:
                    _valControl1 = new TextBox();
                    _valControl2 = new TextBox();
                    comboBox_filterType.Items.AddRange(new string[] {
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEquals.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotEqual.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVBeginsWith.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotBeginWith.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEndsWith.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotEndWith.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVContains.ToString()],
                        AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotContain.ToString()]
                    });
                    break;
            }

            foreach (var item in comboBox_filterType.Items)
            {
                // Do not add "Between" to the second combo if it's there
                if (item.ToString() != AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVBetween.ToString()])
                    comboBox_filterType2.Items.Add(item);
            }

            // Initial selections
            if (initialFilterType != null)
            {
                if (initialFilterType == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVBetween.ToString()])
                {
                    comboBox_filterType.SelectedItem = AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVGreaterThanOrEqualTo.ToString()];
                    comboBox_filterType2.SelectedItem = AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLessThanOrEqualTo.ToString()];
                    radioButton_and.Checked = true;
                }
                else if (comboBox_filterType.Items.Contains(initialFilterType))
                    comboBox_filterType.SelectedItem = initialFilterType;
            }
            else
                comboBox_filterType.SelectedIndex = 0;
            
            if (comboBox_filterType2.SelectedIndex == -1)
                comboBox_filterType2.SelectedIndex = 0;

            // Positioning and Layout
            comboBox_filterType.Location = new Point(10, 30);
            comboBox_filterType.Width = 180;
            _valControl1.Location = new Point(200, 30);
            _valControl1.Width = 180;
            _valControl1.KeyDown += valControl_KeyDown;

            radioButton_and.Location = new Point(40, 65);
            radioButton_or.Location = new Point(100, 65);

            comboBox_filterType2.Location = new Point(10, 95);
            comboBox_filterType2.Width = 180;
            _valControl2.Location = new Point(200, 95);
            _valControl2.Width = 180;
            _valControl2.KeyDown += valControl_KeyDown;

            this.Controls.Add(_valControl1);
            this.Controls.Add(_valControl2);

            errorProvider.SetIconAlignment(_valControl1, ErrorIconAlignment.MiddleRight);
            errorProvider.SetIconPadding(_valControl1, -18);
            errorProvider.SetIconAlignment(_valControl2, ErrorIconAlignment.MiddleRight);
            errorProvider.SetIconPadding(_valControl2, -18);

            // Apply Modern Style
            ApplyModernStyle();
        }

        private void ApplyModernStyle()
        {
            this.BackColor = Color.FromArgb(243, 243, 243);
            this.Font = new Font("Segoe UI", 9f);
            this.ForeColor = Color.Black;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            // Update Labels
            label_columnName.Font = new Font("Segoe UI Semibold", 10f);
            label_columnName.Text = "Filtre ölçütü:";
            this.Text = "Özel Otomatik Filtre";

            // Style ComboBoxes
            comboBox_filterType.BackColor = Color.White;
            comboBox_filterType.FlatStyle = FlatStyle.Flat;

            // Style Buttons
            StyleButton(button_ok, "Tamam", Color.FromArgb(33, 115, 70), Color.White);
            StyleButton(button_cancel, "İptal", Color.White, Color.Black);

            // Adjust sizing and layout
            this.ClientSize = new Size(420, 200);
            
            // Position controls side-by-side
            comboBox_filterType.Location = new Point(10, 30);
            comboBox_filterType.Width = 140;
            if (_valControl1 != null) {
                _valControl1.Location = new Point(160, 30);
                _valControl1.Width = 250;
            }

            radioButton_and.Location = new Point(40, 65);
            radioButton_or.Location = new Point(120, 65);

            comboBox_filterType2.Location = new Point(10, 95);
            comboBox_filterType2.Width = 140;
            if (_valControl2 != null) {
                _valControl2.Location = new Point(160, 95);
                _valControl2.Width = 250;
            }
            
            button_ok.Location = new Point(230, 160);
            button_cancel.Location = new Point(320, 160);
        }

        private void StyleButton(Button btn, string text, Color backColor, Color foreColor)
        {
            btn.Text = text;
            btn.BackColor = backColor;
            btn.ForeColor = foreColor;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.DarkGray;
            btn.Font = new Font("Segoe UI", 9f);
            btn.Size = new Size(80, 28);
            btn.Cursor = Cursors.Hand;
        }

        /// <summary>
        /// Form loaders
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormCustomFilter_Load(object sender, EventArgs e)
        { }

        #endregion


        #region public filter methods

        /// <summary>
        /// Get the Filter string
        /// </summary>
        public string FilterString
        {
            get
            {
                return _filterString;
            }
        }

        /// <summary>
        /// Get the Filter string description
        /// </summary>
        public string FilterStringDescription
        {
            get
            {
                return _filterStringDescription;
            }
        }

        #endregion


        #region filter builder

        private string BuildCustomFilter(FilterType filterType, bool filterDateAndTimeEnabled, string filterTypeConditionText, Control control1)
        {
            string filterString = "";
            string column = "[{0}] ";
            if (filterType == FilterType.Unknown)
                column = "Convert([{0}], 'System.String') ";
            filterString = column;

            switch (filterType)
            {
                case FilterType.DateTime:
                    DateTime dt = ((DateTimePicker)control1).Value;
                    dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
                    if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEquals.ToString()])
                        filterString = "Convert([{0}], 'System.String') LIKE '%" + Convert.ToString((filterDateAndTimeEnabled ? dt : dt.Date), CultureInfo.CurrentCulture) + "%'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEarlierThan.ToString()])
                        filterString += "< '" + Convert.ToString((filterDateAndTimeEnabled ? dt : dt.Date), CultureInfo.CurrentCulture) + "'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEarlierThanOrEqualTo.ToString()])
                        filterString += "<= '" + Convert.ToString((filterDateAndTimeEnabled ? dt : dt.Date), CultureInfo.CurrentCulture) + "'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLaterThan.ToString()])
                        filterString += "> '" + Convert.ToString((filterDateAndTimeEnabled ? dt : dt.Date), CultureInfo.CurrentCulture) + "'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLaterThanOrEqualTo.ToString()])
                        filterString += ">= '" + Convert.ToString((filterDateAndTimeEnabled ? dt : dt.Date), CultureInfo.CurrentCulture) + "'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotEqual.ToString()])
                        filterString = "Convert([{0}], 'System.String') NOT LIKE '%" + Convert.ToString((filterDateAndTimeEnabled ? dt : dt.Date), CultureInfo.CurrentCulture) + "%'";
                    break;
                case FilterType.Integer:
                case FilterType.Float:
                    string num = control1.Text;
                    if (filterType == FilterType.Float) num = num.Replace(",", ".");
                    if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEquals.ToString()])
                        filterString += "= " + num;
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotEqual.ToString()])
                        filterString += "<> " + num;
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVGreaterThan.ToString()])
                        filterString += "> " + num;
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVGreaterThanOrEqualTo.ToString()])
                        filterString += ">= " + num;
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLessThan.ToString()])
                        filterString += "< " + num;
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLessThanOrEqualTo.ToString()])
                        filterString += "<= " + num;
                    break;
                default:
                    string txt = FormatFilterString(control1.Text);
                    if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEquals.ToString()])
                        filterString += "LIKE '" + txt + "'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotEqual.ToString()])
                        filterString += "NOT LIKE '" + txt + "'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVBeginsWith.ToString()])
                        filterString += "LIKE '" + txt + "%'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVEndsWith.ToString()])
                        filterString += "LIKE '%" + txt + "'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotBeginWith.ToString()])
                        filterString += "NOT LIKE '" + txt + "%'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotEndWith.ToString()])
                        filterString += "NOT LIKE '%" + txt + "'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVContains.ToString()])
                        filterString += "LIKE '%" + txt + "%'";
                    else if (filterTypeConditionText == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVDoesNotContain.ToString()])
                        filterString += "NOT LIKE '%" + txt + "%'";
                    break;
            }
            return filterString;
        }

        /// <summary>
        /// Format a text Filter string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private string FormatFilterString(string text)
        {
            string result = "";
            string s;
            string[] replace = { "%", "[", "]", "*", "\"", "\\" };

            for (int i = 0; i < text.Length; i++)
            {
                s = text[i].ToString();
                if (replace.Contains(s))
                    result += "[" + s + "]";
                else
                    result += s;
            }

            return result.Replace("'", "''").Replace("{", "{{").Replace("}", "}}");
        }


        #endregion


        #region buttons events

        /// <summary>
        /// Button Cancel Clieck
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_cancel_Click(object sender, EventArgs e)
        {
            _filterStringDescription = null;
            _filterString = null;
            Close();
        }

        private void button_ok_Click(object sender, EventArgs e)
        {
            // Check for validation errors (only for non-empty fields)
            bool hasError1 = false;
            bool hasError2 = false;

            if (_valControl1 is TextBox && !string.IsNullOrWhiteSpace((_valControl1 as TextBox).Text))
                hasError1 = _valControl1.Tag != null && ((bool)_valControl1.Tag);
            
            if (_valControl2 is TextBox && !string.IsNullOrWhiteSpace((_valControl2 as TextBox).Text))
                hasError2 = _valControl2.Tag != null && ((bool)_valControl2.Tag);

            if (hasError1 || hasError2)
            {
                MessageBox.Show("Lütfen geçerli değerler girin.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Check if at least one value is provided
            bool hasValue1 = false;
            bool hasValue2 = false;

            if (_valControl1 is TextBox)
                hasValue1 = !string.IsNullOrWhiteSpace((_valControl1 as TextBox).Text);
            else if (_valControl1 is DateTimePicker)
                hasValue1 = true;

            if (_valControl2 is TextBox)
                hasValue2 = !string.IsNullOrWhiteSpace((_valControl2 as TextBox).Text);
            else if (_valControl2 is DateTimePicker && _valControl2.Visible)
                hasValue2 = true;

            if (!hasValue1 && !hasValue2)
            {
                MessageBox.Show("Lütfen en az bir değer girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string f1 = "";
            string f2 = "";

            if (hasValue1)
                f1 = BuildCustomFilter(_filterType, _filterDateAndTimeEnabled, comboBox_filterType.Text, _valControl1);

            if (hasValue2 && _valControl2.Visible)
                f2 = BuildCustomFilter(_filterType, _filterDateAndTimeEnabled, comboBox_filterType2.Text, _valControl2);

            if (!String.IsNullOrEmpty(f1) || !String.IsNullOrEmpty(f2))
            {
                if (!String.IsNullOrEmpty(f1) && !String.IsNullOrEmpty(f2))
                {
                    string logic = radioButton_and.Checked ? " AND " : " OR ";
                    _filterString = "(" + f1 + ")" + logic + "(" + f2 + ")";
                    _filterStringDescription = string.Format(AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVFilterStringDescription.ToString()], comboBox_filterType.Text, _valControl1.Text) + 
                                               (radioButton_and.Checked ? " Ve " : " Veya ") + 
                                               string.Format("{0} \"{1}\"", comboBox_filterType2.Text, _valControl2.Text);
                }
                else if (!String.IsNullOrEmpty(f1))
                {
                    _filterString = f1;
                    _filterStringDescription = string.Format(AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVFilterStringDescription.ToString()], comboBox_filterType.Text, _valControl1.Text);
                }
                else
                {
                    _filterString = f2;
                    _filterStringDescription = string.Format(AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVFilterStringDescription.ToString()], comboBox_filterType2.Text, _valControl2.Text);
                }
                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }

            Close();
        }

        #endregion


        #region changed status events

        /// <summary>
        /// Changed condition type
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox_filterType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Between filter shortcut: auto-fill both rows
            if (comboBox_filterType.Text == AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVBetween.ToString()])
            {
                comboBox_filterType.Text = AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVGreaterThanOrEqualTo.ToString()];
                comboBox_filterType2.Text = AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVLessThanOrEqualTo.ToString()];
                radioButton_and.Checked = true;
            }
            
            // Enable OK button based on validation state
            button_ok.Enabled = !(_valControl1.Visible && _valControl1.Tag != null && ((bool)_valControl1.Tag)) ||
                (_valControl2.Visible && _valControl2.Tag != null && ((bool)_valControl2.Tag));
        }

        /// <summary>
        /// Changed a control Text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void valControl_TextChanged(object sender, EventArgs e)
        {
            bool hasErrors = false;
            switch (_filterType)
            {
                case FilterType.Integer:
                    Int64 val;
                    hasErrors = !(Int64.TryParse((sender as TextBox).Text, out val));
                    break;

                case FilterType.Float:
                    Double val1;
                    hasErrors = !(Double.TryParse((sender as TextBox).Text, out val1));
                    break;
            }

            (sender as Control).Tag = hasErrors || (sender as TextBox).Text.Length == 0;

            if (hasErrors && (sender as TextBox).Text.Length > 0)
                errorProvider.SetError((sender as Control), AdvancedDataGridView.Translations[AdvancedDataGridView.TranslationKey.ADGVInvalidValue.ToString()]);
            else
                errorProvider.SetError((sender as Control), "");

            button_ok.Enabled = !(_valControl1.Visible && _valControl1.Tag != null && ((bool)_valControl1.Tag)) ||
                (_valControl2.Visible && _valControl2.Tag != null && ((bool)_valControl2.Tag));
        }

        /// <summary>
        /// KeyDown on a control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void valControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter)
            {
                if (sender == _valControl1)
                {
                    if (_valControl2.Visible)
                        _valControl2.Focus();
                    else
                        button_ok_Click(button_ok, new EventArgs());
                }
                else
                {
                    button_ok_Click(button_ok, new EventArgs());
                }

                e.SuppressKeyPress = false;
                e.Handled = true;
            }
        }

        #endregion

    }
}

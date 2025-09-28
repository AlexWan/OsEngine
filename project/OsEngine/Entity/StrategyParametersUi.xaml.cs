/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.Layout;
using System.Threading.Tasks;
using System.IO;

namespace OsEngine.Entity
{
    public partial class StrategyParametersUi
    {
        private List<IIStrategyParameter> _parameters;

        private BotPanel _panel;

        private bool _isParametersUiClosed;

        public StrategyParametersUi(List<IIStrategyParameter> parameters, ParamGuiSettings settings, BotPanel panel)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            Height = (double)settings.Height;
            Width = (double)settings.Width;

            _parameters = parameters;
            _panel = panel;

            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;
            ButtonUpdate.Content = OsLocalization.Entity.ButtonUpdate;
            ButtonSaveSettings.Content = OsLocalization.Entity.ButtonSave;
            ButtonLoadSettings.Content = OsLocalization.Entity.ButtonLoad;

            if (string.IsNullOrEmpty(settings.Title))
            {
                Title = OsLocalization.Entity.TitleParametersUi;
            }
            else
            {
                Title = settings.Title;
            }

            if(string.IsNullOrEmpty(panel.PublicName) == false)
            {
                Title += " / " + panel.PublicName;
            }
            else
            {
                Title += " / " + panel.NameStrategyUniq;
            }

            List<List<IIStrategyParameter>> sorted = GetParamSortedByTabName();

            for(int i = 0;i < sorted.Count;i++)
            {
                if(sorted[i][0].TabName == null)
                {
                    CreateTab(sorted[i], settings.FirstTabLabel);
                }
                else
                {
                    CreateTab(sorted[i], sorted[i][0].TabName);
                }
            }
            
            for(int i = 0;i < settings.CustomTabs.Count;i++)
            {
                CreateCustomTab(settings.CustomTabs[i]);
            }

            RePaintParameterTablesAsync();

            this.Closed += StrategyParametersUi_Closed;

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "botPanelParameters_" + panel.NameStrategyUniq);
        }

        private void StrategyParametersUi_Closed(object sender, EventArgs e)
        {
            try
            {
                this.Closed -= StrategyParametersUi_Closed;
                _parameters = null;

                _isParametersUiClosed = true;

                if (_tabs != null)
                {
                    for (int i = 0; i < _tabs.Count; i++)
                    {
                        _tabs[i].Dispose();
                        _tabs[i].ErrorEvent -= Painter_ErrorEvent;
                    }

                    _tabs.Clear();
                    _tabs = null;
                }

                _panel = null;
            }
            catch(Exception ex)
            {
                if(_panel != null)
                {
                    _panel.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
                    _panel = null;
                }
            }
        }

        private List<List<IIStrategyParameter>> GetParamSortedByTabName()
        {
            List<List<IIStrategyParameter>> sorted = new List<List<IIStrategyParameter>>();

            for(int i = 0;i < _parameters.Count;i++)
            {
                List<IIStrategyParameter> myList = sorted.Find(s => s[0].TabName == _parameters[i].TabName);

                if(myList != null)
                {
                    myList.Add(_parameters[i]);
                }
                else
                {
                    List<IIStrategyParameter> newItem = new List<IIStrategyParameter>();
                    newItem.Add(_parameters[i]);
                    sorted.Add(newItem);
                }
            }

            for(int i = 0;i < sorted.Count;i++)
            {// переставляем принудительно параметры без имени вкладки в первый слот вкладок
                if(sorted[i][0].TabName == null && i != 0)
                {
                    List<IIStrategyParameter> par = sorted[i];
                    sorted.RemoveAt(i);
                    sorted.Insert(0, par);
                    break;
                }
            }

            return sorted;
        }

        private void CreateCustomTab(CustomTabToParametersUi tab)
        {
            TabItem item = new TabItem();
            item.Header = tab.Label;
            item.Content = tab.GridToPaint;

            TabControlSettings.Items.Add(item);
        }

        private void CreateTab(List<IIStrategyParameter> par, string tabName)
        {
            try
            {
                ParamTabPainter painter = new ParamTabPainter(par, tabName, TabControlSettings, _panel.ParamGuiSettings); 
                painter.ErrorEvent += Painter_ErrorEvent;
                _tabs.Add(painter);
            }
            catch(Exception ex)
            {
                _panel?.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        private List<ParamTabPainter> _tabs = new List<ParamTabPainter>();

        private void ButtonAccept_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    _tabs[i].Save();
                }

                Close();
            }
            catch (Exception ex)
            {
                _panel?.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonUpdate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    _tabs[i].Save();
                }
            }
            catch (Exception ex)
            {
                _panel?.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void Painter_ErrorEvent(string error)
        {
            _panel?.SendNewLogMessage(error,Logging.LogMessageType.Error);
        }
		
        private async void RePaintParameterTablesAsync()
        {
            bool _rePaint = _panel.ParamGuiSettings.IsRePaintParameterTables;

            while (true)
            {
                try
                {
                    if (_panel?.ParamGuiSettings == null)
                    {
                        StrategyParametersUi_Closed(null, null);
                        Close();
                        return;
                    }
					
                    if (_isParametersUiClosed == true)
                    {
                        return;
                    }
                    
                    if (_rePaint != _panel?.ParamGuiSettings.IsRePaintParameterTables)
                    {
                        _rePaint = _panel.ParamGuiSettings.IsRePaintParameterTables;

                        for (int i = 0; i < _tabs.Count; i++)
                        {
                            _tabs[i]?.PaintTable();                        
                        }
                    }
                }
                catch (Exception ex)
                {
                    _panel?.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                    return;
                }

                await Task.Delay(1000);
            }
        }

        private void ButtonSaveSettings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
                saveFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";

                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.ShowDialog();

                if (string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    return;
                }

                string filePath = saveFileDialog.FileName;

                if (File.Exists(filePath) == false)
                {
                    using (FileStream stream = File.Create(filePath))
                    {
                        // do nothin
                    }
                }

                try
                {
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        writer.WriteLine(GetSaveParamString());
                    }
                }
                catch (Exception error)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                    ui.ShowDialog();
                }
            }
            catch
            {
                // ignore
            }
        }

        private void ButtonLoadSettings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.ShowDialog();

                if (string.IsNullOrEmpty(openFileDialog.FileName))
                {
                    return;
                }

                string filePath = openFileDialog.FileName;

                if (File.Exists(filePath) == false)
                {
                    return;
                }

                try
                {
                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        string fileStr = reader.ReadToEnd();
                        LoadParametersFromString(fileStr);
                    }
                }
                catch (Exception error)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                    ui.ShowDialog();
                }
            }
            catch
            {
                // ignore
            }
        }

        private string GetSaveParamString()
        {
            string result = "";

            for(int i = 0; i < _parameters.Count; i++)
            {
                IIStrategyParameter parameter = _parameters[i];

                if (parameter.Type == StrategyParameterType.Button
                    || parameter.Type == StrategyParameterType.Label)
                {
                    continue;
                }
                else if (parameter.Type == StrategyParameterType.Bool)
                {
                    StrategyParameterBool parameterBool = (StrategyParameterBool)parameter;

                    result += 
                        StrategyParameterType.Bool.ToString() + "#"
                        + parameterBool.Name + "#"
                        + parameterBool.ValueBool + "#"
                        + "\n";
                }
                else if(parameter.Type == StrategyParameterType.CheckBox)
                {
                    StrategyParameterCheckBox parameterCheckBox = (StrategyParameterCheckBox)parameter;

                    result +=
                        StrategyParameterType.DecimalCheckBox.ToString() + "#"
                        + parameterCheckBox.Name + "#"
                        + parameterCheckBox.CheckState + "#"
                        + "\n";
                }
                else if(parameter.Type == StrategyParameterType.Decimal)
                {
                    StrategyParameterDecimal parameterDecimal = (StrategyParameterDecimal)parameter;

                    result +=
                        StrategyParameterType.Decimal.ToString() + "#"
                        + parameterDecimal.Name + "#"
                        + parameterDecimal.ValueDecimal + "#"
                        + "\n";
                }
                else if(parameter.Type == StrategyParameterType.DecimalCheckBox)
                {
                    StrategyParameterDecimalCheckBox parameterCheckBoxDecimal = (StrategyParameterDecimalCheckBox)parameter;

                    result +=
                        StrategyParameterType.DecimalCheckBox.ToString() + "#"
                        + parameterCheckBoxDecimal.Name + "#"
                        + parameterCheckBoxDecimal.ValueDecimal + "#"
                        + parameterCheckBoxDecimal.CheckState + "#"
                        + "\n";
                }
                else if(parameter.Type == StrategyParameterType.Int)
                {
                    StrategyParameterInt parameterInt = (StrategyParameterInt)parameter;

                    result +=
                        StrategyParameterType.Int.ToString() + "#"
                        + parameterInt.Name + "#"
                        + parameterInt.ValueInt + "#"
                        + "\n";
                }
                else if (parameter.Type == StrategyParameterType.String)
                {
                    StrategyParameterString parameterString = (StrategyParameterString)parameter;

                    result +=
                        StrategyParameterType.String.ToString() + "#"
                        + parameterString.Name + "#"
                        + parameterString.ValueString + "#"
                        + "\n";
                }
                else if (parameter.Type == StrategyParameterType.TimeOfDay)
                {
                    StrategyParameterTimeOfDay parameterTimeOfDay = (StrategyParameterTimeOfDay)parameter;

                    result +=
                        StrategyParameterType.TimeOfDay.ToString() + "#"
                        + parameterTimeOfDay.Name + "#"
                        + parameterTimeOfDay.Value + "#"
                        + "\n";
                }
            }

            return result;
        }

        private void LoadParametersFromString(string parametersString)
        {
            string[] rows = parametersString.Split('\n');

            for(int i = 0;i < rows.Length;i++)
            {
                string[] parameter = rows[i].Split('#');

                for(int i2 = 0; i2< _tabs.Count; i2++)
                {
                    _tabs[i2].LoadParameterOnTable(parameter);
                }
            }
        }
    }

    public class ParamTabPainter
    {
        public ParamTabPainter(List<IIStrategyParameter> parameters, 
            string tabName, System.Windows.Controls.TabControl tabControl, ParamGuiSettings parametersGuiSettings)
        {
            TabItem item = new TabItem();
            item.Header = tabName;
            _host = new WindowsFormsHost();

            item.Content = _host;

            tabControl.Items.Add(item);
            _parameters = parameters;

            _parametersGuiSettings = parametersGuiSettings;
			
            CreateTable();
            PaintTable();
        }

        public void Dispose()
        {
            try
            {
                if (_grid != null
                     && _grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(Dispose));
                    return;
                }

                _parameters = null;

                if (_host != null)
                {
                    _host.Child = null;
                    _host = null;
                }

                if (_grid != null)
                {
                    _grid.CellValueChanged -= _grid_CellValueChanged;
                    _grid.CellClick -= _grid_Click;
                    _grid.DataError -= _grid_DataError;

                    _grid.RowPostPaint -= _grid_RowPostPaint;         
                    _grid.CellFormatting -= _grid_CellFormatting;     
                    _grid.CellPainting -= _grid_CellPainting;   

                    _grid.Rows.Clear();
                    DataGridFactory.ClearLinks(_grid);
                    _grid = null;
                }
            }
            catch
            {
                // ignore
            }
        }

        private List<IIStrategyParameter> _parameters;

        private WindowsFormsHost _host;

        private DataGridView _grid;
		
        private ParamGuiSettings _parametersGuiSettings;				

        private void CreateTable()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            _grid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Entity.ParametersColumn1;
            column0.ReadOnly = true;
            column0.Width = 250;

            _grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Entity.ParametersColumn2;
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column1);

            _grid.Rows.Add(null, null);

            _grid.CellValueChanged += _grid_CellValueChanged;
            _grid.CellClick += _grid_Click;
            _grid.DataError += _grid_DataError;

            _grid.RowPostPaint += _grid_RowPostPaint;
            _grid.CellFormatting += _grid_CellFormatting;
            _grid.CellPainting += _grid_CellPainting;

            _host.Child = _grid;
        }
		
        public void PaintTable()	
        {
            _grid.Rows.Clear();

            for (int i = 0; i < _parameters.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = _parameters[i].Name;

                if (_parameters[i].Type == StrategyParameterType.Bool)
                {
                    DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();

                    cell.Items.Add("False");
                    cell.Items.Add("True");
                    cell.Value = ((StrategyParameterBool)_parameters[i]).ValueBool.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.String)
                {
                    StrategyParameterString param = (StrategyParameterString)_parameters[i];

                    if (param.ValuesString.Count > 1)
                    {
                        DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();

                        bool isInArray = false;

                        for (int i2 = 0; i2 < param.ValuesString.Count; i2++)
                        {
                            cell.Items.Add(param.ValuesString[i2]);

                            if(param.ValueString == param.ValuesString[i2])
                            {
                                isInArray = true;
                            }
                        }

                        if(isInArray)
                        {
                            cell.Value = param.ValueString;
                        }
                        else
                        {
                            param.ValueString = param.ValuesString[0];
                            cell.Value = param.ValueString;
                        }

                        row.Cells.Add(cell);
                    }
                    else if (param.ValuesString.Count == 1
                        || (param.ValuesString.Count == 0 && param.ValueString != null))
                    {
                        DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                        cell.Value = param.ValueString;
                        row.Cells.Add(cell);
                    }
                }
                else if (_parameters[i].Type == StrategyParameterType.Int)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();

                    StrategyParameterInt param = (StrategyParameterInt)_parameters[i];

                    cell.Value = param.ValueInt.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.Decimal)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();

                    StrategyParameterDecimal param = (StrategyParameterDecimal)_parameters[i];

                    cell.Value = param.ValueDecimal.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.TimeOfDay)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();

                    StrategyParameterTimeOfDay param = (StrategyParameterTimeOfDay)_parameters[i];

                    cell.Value = param.Value.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.Button)
                {
                    DataGridViewButtonCell cell = new DataGridViewButtonCell();
                    row.Cells[0].Value = "";
                    cell.Value = _parameters[i].Name;
                    // StrategyParameterButton param = (StrategyParameterButton)_parameters[i];

                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.Label)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    StrategyParameterLabel param = (StrategyParameterLabel)_parameters[i];

                    if (param.RowHeight == 0)
                    {
                        param.RowHeight = 25;
                    }

                    row.Cells[0].Value = param.Label;
                    
                    row.Height = param.RowHeight;
                    
                    row.Cells[0].Style.Font = new System.Drawing.Font("Areal", param.TextHeight);
                    row.Cells[0].Style.ForeColor = param.Color;

                    row.Cells.Add(cell);
                    row.Cells[1].Value = param.Value;
                    row.Cells[1].Style.Font = new System.Drawing.Font("Areal", param.TextHeight);
                    row.Cells[1].Style.ForeColor = param.Color;

                    
                }
                else if (_parameters[i].Type == StrategyParameterType.CheckBox)
                {
                    DataGridViewCheckBoxCell cell = new DataGridViewCheckBoxCell();
                    StrategyParameterCheckBox param = (StrategyParameterCheckBox)_parameters[i];

                    row.Cells[0].Value = _parameters[i].Name; 
                    cell.Value = param.CheckState;

                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.DecimalCheckBox)
                {
                    if (_grid.ColumnCount == 2)
                    {
                        DataGridViewCell cell = new DataGridViewTextBoxCell();

                        DataGridViewColumn column = new DataGridViewColumn(cell); 
                        column.Width = 20;

                        _grid.Columns.Add(column);   
                    }

                    StrategyParameterDecimalCheckBox param = (StrategyParameterDecimalCheckBox)_parameters[i];

                    DataGridViewTextBoxCell cell1 = new DataGridViewTextBoxCell();
                    cell1.Value = param.ValueDecimal.ToString();

                    DataGridViewCheckBoxCell cell2 = new DataGridViewCheckBoxCell();
                    cell2.Value = param.CheckState;

                    row.Cells.Add(cell1);
                    row.Cells.Add(cell2);
                }

                _grid.Rows.Add(row);
            }
        }

        private void _grid_Click(object sender, EventArgs e)
        {
            int index = 0;

            try
            {
                int cellIndex = _grid.SelectedCells[0].ColumnIndex;

                if (cellIndex != 1)
                {
                    return;
                }

                index = _grid.SelectedCells[0].RowIndex;
                if (_parameters[index].Type != StrategyParameterType.Button)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            try
            {
                StrategyParameterButton param = (StrategyParameterButton)_parameters[index];
                param.Click();
            }
            catch(Exception ex)
            {
                if (ErrorEvent != null)
                {
                    ErrorEvent(ex.ToString());
                }
            }
        }

        private void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            int index = e.RowIndex;

            if (_parameters[index].Type != StrategyParameterType.TimeOfDay)
            {
                return;
            }

            StrategyParameterTimeOfDay param = new StrategyParameterTimeOfDay("temp", 0, 0, 0, 0);

            try
            {
                string[] array = new[] { "", _grid.Rows[index].Cells[1].EditedFormattedValue.ToString() };
                param.LoadParamFromString(array);
            }
            catch
            {
                _grid.Rows[index].Cells[1].Value = ((StrategyParameterTimeOfDay)_parameters[index]).Value.ToString();
            }
        }

        private void _grid_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            try
            {
                if (_parametersGuiSettings.ParameterDesigns.Count != 0)
                {
                    string paramName = _grid.Rows[e.RowIndex].Cells[0].Value?.ToString();

                    if (paramName == "")
                    {
                        paramName = _grid.Rows[e.RowIndex].Cells[1].Value?.ToString();
                    }

                    string key = paramName + ParamDesignType.BorderUnder.ToString();

                    if (_parametersGuiSettings.ParameterDesigns.ContainsKey(key))
                    {
                        int thickness = _parametersGuiSettings.ParameterDesigns[key].Thickness;
                        int editThickness;

                        if (thickness < 1)
                        {
                            editThickness = 1;
                        }
                        else if (thickness > 10)
                        {
                            editThickness = 10;
                        }
                        else
                        {
                            editThickness = thickness;
                        }

                        using (System.Drawing.Pen pen = new System.Drawing.Pen(_parametersGuiSettings.ParameterDesigns[key].Color, editThickness))
                        {
                            int y = e.RowBounds.Bottom - 1;
                            e.Graphics.DrawLine(pen, e.RowBounds.Left, y, e.RowBounds.Right, y);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ErrorEvent != null)
                {
                    ErrorEvent(ex.ToString());
                }
            }
        }

        private void _grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {   
            try
            {
                if (_parametersGuiSettings.ParameterDesigns.Count != 0 && e.Value != null)
                {
                    string paramName = _grid.Rows[e.RowIndex].Cells[0].Value?.ToString();

                    if (paramName == "")
                    {
                        paramName = _grid.Rows[e.RowIndex].Cells[1].Value?.ToString();
                    }

                    string key = paramName + ParamDesignType.ForeColor.ToString();

                    if (_parametersGuiSettings.ParameterDesigns.ContainsKey(key))
                    {
                        e.CellStyle.ForeColor = _parametersGuiSettings.ParameterDesigns[key].Color;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ErrorEvent != null)
                {
                    ErrorEvent(ex.ToString());
                }
            }
        }

        private void _grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            try
            {
                if (_parametersGuiSettings.ParameterDesigns.Count != 0)
                {
                    if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected)
                    {
                        string paramName = _grid.Rows[e.RowIndex].Cells[0].Value?.ToString();

                        if (paramName == "")
                        {
                            paramName = _grid.Rows[e.RowIndex].Cells[1].Value?.ToString();
                        }

                        string key = paramName + ParamDesignType.SelectionColor.ToString();

                        if (_parametersGuiSettings.ParameterDesigns.ContainsKey(key))
                        {
                            e.CellStyle.SelectionForeColor = _parametersGuiSettings.ParameterDesigns[key].Color;

                            e.PaintBackground(e.CellBounds, true);
                            e.PaintContent(e.CellBounds);
                            e.Handled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ErrorEvent != null)
                {
                    ErrorEvent(ex.ToString());
                }
            }
        }

        public void Save()
        {
            for (int i = 0; i < _parameters.Count; i++)
            {
                try
                {
                    if (_parameters[i].Type == StrategyParameterType.String)
                    {
                        ((StrategyParameterString)_parameters[i]).ValueString = _grid.Rows[i].Cells[1].EditedFormattedValue.ToString();
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Int)
                    {
                        ((StrategyParameterInt)_parameters[i]).ValueInt = Convert.ToInt32(_grid.Rows[i].Cells[1].EditedFormattedValue.ToString());
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Bool)
                    {
                        ((StrategyParameterBool)_parameters[i]).ValueBool = Convert.ToBoolean(_grid.Rows[i].Cells[1].EditedFormattedValue.ToString());
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Decimal)
                    {
                        ((StrategyParameterDecimal)_parameters[i]).ValueDecimal = _grid.Rows[i].Cells[1].EditedFormattedValue.ToString().ToDecimal();
                    }
                    else if (_parameters[i].Type == StrategyParameterType.TimeOfDay)
                    {
                        string[] array = new[] { "", _grid.Rows[i].Cells[1].EditedFormattedValue.ToString() };
                        ((StrategyParameterTimeOfDay)_parameters[i]).LoadParamFromString(array);
                    }
                    else if (_parameters[i].Type == StrategyParameterType.CheckBox)
                    {
                        bool value = Convert.ToBoolean(_grid.Rows[i].Cells[1].Value);

                        if (value == true)
                        {
                            ((StrategyParameterCheckBox) _parameters[i]).CheckState = CheckState.Checked;
                        }
                        else
                        {
                            ((StrategyParameterCheckBox)_parameters[i]).CheckState = CheckState.Unchecked;
                        }
                    }
                    else if (_parameters[i].Type == StrategyParameterType.DecimalCheckBox)
                    {
                        ((StrategyParameterDecimalCheckBox)_parameters[i]).ValueDecimal = _grid.Rows[i].Cells[1].EditedFormattedValue.ToString().ToDecimal();

                        bool value = Convert.ToBoolean(_grid.Rows[i].Cells[2].Value);

                        if (value == true)
                        {
                            ((StrategyParameterDecimalCheckBox)_parameters[i]).CheckState = CheckState.Checked;
                        }
                        else
                        {
                            ((StrategyParameterDecimalCheckBox)_parameters[i]).CheckState = CheckState.Unchecked;
                        }
                    }				
                }
                catch(Exception ex) 
                {
                    if (ErrorEvent != null)
                    {
                        ErrorEvent("Parameters window exception:\n" 
                            + _parameters[i].Name + "\n"
                            + ex.ToString());
                    }
                    return;
                }

            }
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (ErrorEvent != null)
            {
                ErrorEvent("Parameters window exception: " + e.ToString());
            }
        }

        public void LoadParameterOnTable(string[] parameter)
        {
            if(_grid == null 
                || _grid.Rows == null 
                || _grid.Rows.Count == 0)
            {
                return;
            }

            if(parameter.Length < 2)
            {
                return;
            }

            string parameterType = parameter[0];
            string parameterName = parameter[1];
            
            if(parameterName == "")
            {
                return;
            }

            for(int i = 0;i < _grid.Rows.Count;i++)
            {
                DataGridViewRow row = _grid.Rows[i];

                if (row.Cells == null 
                    || row.Cells.Count == 0 
                    || row.Cells[0].Value == null)
                {
                    continue;
                }

                string gridName = row.Cells[0].Value.ToString();

                if(parameterName != gridName)
                {
                    continue;
                }

                if (parameterType == StrategyParameterType.Bool.ToString())
                {
                    DataGridViewComboBoxCell cell = (DataGridViewComboBoxCell)row.Cells[1];
                    cell.Value = parameter[2];
                }
                else if (parameterType == StrategyParameterType.CheckBox.ToString())
                {
                    DataGridViewComboBoxCell cell = (DataGridViewComboBoxCell)row.Cells[1];
                    cell.Value = parameter[2];
                }
                else if (parameterType == StrategyParameterType.Decimal.ToString())
                {
                    DataGridViewTextBoxCell cell = (DataGridViewTextBoxCell)row.Cells[1];
                    cell.Value = parameter[2];
                }
                else if (parameterType == StrategyParameterType.DecimalCheckBox.ToString())
                {
                    DataGridViewTextBoxCell cell1 =  (DataGridViewTextBoxCell)row.Cells[1];
                    cell1.Value = parameter[2];

                    DataGridViewCheckBoxCell cell2 = (DataGridViewCheckBoxCell)row.Cells[2];
                    cell2.Value = parameter[3];
                }
                else if (parameterType == StrategyParameterType.Int.ToString())
                {
                    DataGridViewTextBoxCell cell = (DataGridViewTextBoxCell)row.Cells[1];
                    cell.Value = parameter[2];
                }
                else if (parameterType == StrategyParameterType.String.ToString())
                {
                    DataGridViewCell cell = (DataGridViewCell)row.Cells[1];
                    cell.Value = parameter[2];
                }
                else if (parameterType == StrategyParameterType.TimeOfDay.ToString())
                {
                    DataGridViewTextBoxCell cell = (DataGridViewTextBoxCell)row.Cells[1];
                    cell.Value = parameter[2];
                }

                return;

            }
        }

        public event Action<string> ErrorEvent;
    }
}
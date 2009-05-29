using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using HeavyDuck.Utilities.Forms;

namespace HeavyDuck.Dnd.Forms
{
    public partial class MonsterSearchDialog : Form
    {
        private const int MIN_QUERY_LENGTH = 2;

        private CompendiumHelper m_helper = null;

        public MonsterSearchDialog(CompendiumHelper helper)
        {
            InitializeComponent();

            m_helper = helper;

            this.Load += new EventHandler(MonsterSearchDialog_Load);
            name_box.KeyUp += new KeyEventHandler(name_box_KeyUp);
            name_box.TextChanged += new EventHandler(name_box_TextChanged);
            search_button.Click += new EventHandler(search_button_Click);
        }

        #region Event Handlers

        private void MonsterSearchDialog_Load(object sender, EventArgs e)
        {
            // set up grid
            GridHelper.Initialize(grid, true);
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            GridHelper.AddColumn(grid, "Level", "L");
            GridHelper.AddColumn(grid, "Name", "Name");
            GridHelper.AddColumn(grid, "CombatRole", "Combat Role");
            GridHelper.AddColumn(grid, "GroupRole", "Group Role");
            GridHelper.AddColumn(grid, "SourceBook", "Source");
            grid.Columns["Level"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns["Name"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns["Name"].FillWeight = 2;
            grid.Columns["SourceBook"].DefaultCellStyle.Font = new Font(grid.DefaultCellStyle.Font, FontStyle.Italic);
            grid.Columns["SourceBook"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns["SourceBook"].FillWeight = 1;
            grid.CurrentCellChanged += new EventHandler(grid_CurrentCellChanged);

            // initialize UI state
            UpdateButtons();
        }

        private void grid_CurrentCellChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void name_box_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && name_box.Text.Trim().Length >= MIN_QUERY_LENGTH)
                search_button_Click(sender, e);
            else if (e.KeyCode == Keys.Escape)
                name_box.Text = "";
        }

        private void name_box_TextChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void search_button_Click(object sender, EventArgs e)
        {
            ProgressDialog progress;
            DataTable results = null;

            progress = new ProgressDialog();
            progress.AutoAdvance = true;
            progress.AddTask((p) =>
            {
                p.Update("Logging in...");

                if (!m_helper.ValidateCookies())
                    m_helper.Login();
            });
            progress.AddTask((p) =>
            {
                p.Update("Querying compendium...");

                // create the data table
                results = new DataTable("Monsters");
                results.Columns.Add("ID", typeof(int));
                results.Columns.Add("Name", typeof(string));
                results.Columns.Add("Level", typeof(int));
                results.Columns.Add("GroupRole", typeof(string));
                results.Columns.Add("CombatRole", typeof(string));
                results.Columns.Add("SourceBook", typeof(string));
                results.BeginLoadData();

                // query the web service, create the document, and select nodes
                using (Stream s = m_helper.SearchMonsters(name_box.Text))
                {
                    XPathDocument doc = new XPathDocument(s);
                    XPathNavigator nav = doc.CreateNavigator();
                    XPathNodeIterator iter = nav.Select("/Data/Results/Monster");

                    // load the rows
                    while (iter.MoveNext())
                    {
                        results.LoadDataRow(new object[] {
                        iter.Current.SelectSingleNode("ID").ValueAsInt,
                        iter.Current.SelectSingleNode("Name").Value,
                        iter.Current.SelectSingleNode("Level").ValueAsInt,
                        iter.Current.SelectSingleNode("GroupRole").Value,
                        iter.Current.SelectSingleNode("CombatRole").Value,
                        iter.Current.SelectSingleNode("SourceBook").Value,
                    }, false);
                    }
                }

                // complete load
                results.AcceptChanges();
                results.DefaultView.Sort = "Level ASC, Name ASC";
                results.EndLoadData();
            });

            try
            {
                progress.Show();

                grid.DataSource = results;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Public Properties

        public int SelectedMonsterID
        {
            get
            {
                DataRow row = GetCurrentRow();

                // return an invalid ID if nothing was selected
                if (row == null) return -1;

                // return the value
                return Convert.ToInt32(row["ID"]);
            }
        }

        public string SelectedMonsterName
        {
            get
            {
                DataRow row = GetCurrentRow();

                // return an invalid ID if nothing was selected
                if (row == null) return null;

                // return the value
                return row["Name"].ToString();
            }
        }

        #endregion

        #region Private Methods

        private void UpdateButtons()
        {
            ok_button.Enabled = grid.CurrentRow != null;
            search_button.Enabled = name_box.Text.Length >= MIN_QUERY_LENGTH;
        }

        private DataRow GetCurrentRow()
        {
            if (grid.CurrentRow == null)
                return null;
            else
                return ((DataRowView)grid.CurrentRow.DataBoundItem).Row;
        }

        #endregion
    }
}

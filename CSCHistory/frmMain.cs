using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSCHistory
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }

        EDIEntities EDIEntities = new EDIEntities();    

        private void Form1_Load(object sender, EventArgs e)
        {
            var sData = (from o in EDIEntities.orderfiles 
                         join s in EDIEntities.Scores on o.ID equals s.orderfileID
                         join i in EDIEntities.SpecialInstructions on o.ID equals i.orderfileID
                         select new
                         {
                             o.ID

                         })
                         .ToList();

            dataGridView1.DataSource = sData;
        }
    }
}

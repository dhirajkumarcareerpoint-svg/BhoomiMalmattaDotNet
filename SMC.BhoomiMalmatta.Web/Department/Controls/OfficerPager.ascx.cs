using System;
using System.Web.UI;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Web.Department.Controls
{
    public partial class OfficerPager : UserControl
    {
        public event EventHandler FirstPage, PreviousPage, NextPage, LastPage;
        public void Show(PagerSummary summary)
        {
            lblRange.Text = summary.RangeText; lblPage.Text = summary.PageText;
            btnFirst.Enabled = btnPrevious.Enabled = summary.Page > 1;
            btnNext.Enabled = btnLast.Enabled = summary.Page < summary.TotalPages;
        }
        protected void First_Click(object sender, EventArgs e) { if (FirstPage != null) FirstPage(this, EventArgs.Empty); }
        protected void Previous_Click(object sender, EventArgs e) { if (PreviousPage != null) PreviousPage(this, EventArgs.Empty); }
        protected void Next_Click(object sender, EventArgs e) { if (NextPage != null) NextPage(this, EventArgs.Empty); }
        protected void Last_Click(object sender, EventArgs e) { if (LastPage != null) LastPage(this, EventArgs.Empty); }
    }
}

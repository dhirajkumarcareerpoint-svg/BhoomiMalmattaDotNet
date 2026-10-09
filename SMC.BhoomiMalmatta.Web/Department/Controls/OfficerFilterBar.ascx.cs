using System; using System.Web.UI; using System.Web.UI.WebControls;
namespace SMC.BhoomiMalmatta.Web.Department.Controls
{
 public partial class OfficerFilterBar : UserControl
 {
  public event EventHandler SearchRequested, ResetRequested;
  public string SearchText { get { return txtSearch.Text; } } public string FromText { get { return txtFrom.Text; } } public string ToText { get { return txtTo.Text; } }
  public DropDownList StatusList { get { return ddlStatus; } } public DropDownList StageList { get { return ddlStage; } } public DropDownList ServiceList { get { return ddlService; } } public DropDownList ZoneList { get { return ddlZone; } } public DropDownList RouteList { get { return ddlRoute; } } public PlaceHolder ZonePanel { get { return phZone; } }
  public void ShowAdvanced(bool visible) { phAdvanced.Visible = visible; }
  public void AddStatus(string text,string value){ddlStatus.Items.Add(new ListItem(text,value));} public void AddStage(string text,string value){ddlStage.Items.Add(new ListItem(text,value));} public void AddService(string text,string value){ddlService.Items.Add(new ListItem(text,value));} public void AddZone(string text,string value){ddlZone.Items.Add(new ListItem(text,value));} public void AddRoute(string text,string value){ddlRoute.Items.Add(new ListItem(text,value));}
  public void SelectStatus(string value){Select(ddlStatus,value);} public void SelectStage(string value){Select(ddlStage,value);} public void SelectRoute(string value){Select(ddlRoute,value);} private static void Select(DropDownList d,string v){if(v!=null&&d.Items.FindByValue(v)!=null)d.SelectedValue=v;}
  protected void Search_Click(object sender,EventArgs e){if(SearchRequested!=null)SearchRequested(this,EventArgs.Empty);} protected void Reset_Click(object sender,EventArgs e){if(ResetRequested!=null)ResetRequested(this,EventArgs.Empty);}
 }
}
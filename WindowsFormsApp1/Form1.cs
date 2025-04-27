// in case Rain sees this I tried my best to keep everything camelCase we love Joe Camel
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private List<ListViewItem> allListViewItems;
        // stolen from stack overflow

        private Point mouseOffset;
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
(
    int nLeftRect,     // x-coordinate of upper-left corner
    int nTopRect,      // y-coordinate of upper-left corner
    int nRightRect,    // x-coordinate of lower-right corner
    int nBottomRect,   // y-coordinate of lower-right corner
    int nWidthEllipse, // width of ellipse
    int nHeightEllipse // height of ellipse
);
        public void loadProvidersIntoListView()
        {
            listView1.View = View.Details;
            listView1.CheckBoxes = true;
            listView1.Columns.Clear();
            listView1.Columns.Add("Checkbox", -2, HorizontalAlignment.Left);
            listView1.Columns.Add("Name", -2, HorizontalAlignment.Left);
            listView1.Columns.Add("GUID", -2, HorizontalAlignment.Left);
            listView1.Items.Clear();
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = @"C:\Windows\System32\logman.exe",
                Arguments = "query providers",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    List<providerInfo> providers = parseLogmanOutput(output);
                    foreach (providerInfo provider in providers)
                    {
                        ListViewItem item = new ListViewItem
                        {
                            Checked = false,
                            Text = ""
                        };
                        item.SubItems.Add(provider.Name);
                        item.SubItems.Add(provider.Guid);
                        listView1.Items.Add(item);
                    }
                    // The command completed succcessfully appears as an entry if I doont do this LMAO
                    if (listView1.Items.Count > 0)
                    {
                        listView1.Items.RemoveAt(listView1.Items.Count - 1);
                    }

                    listView1.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                    listView1.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                    allListViewItems = listView1.Items.Cast<ListViewItem>().ToList();
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Error querying providers: {ex.Message}");
            }
        }

        private List<providerInfo> parseLogmanOutput(string output)
        {
            List<providerInfo> providers = new List<providerInfo>();
            string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                if (line.Contains("Provider") && line.Contains("GUID"))
                    continue;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 2)
                {
                    string guid = parts[parts.Length - 1].Trim();
                    string name = string.Join(" ", parts, 0, parts.Length - 1).Trim().Trim('-'); // the first entry without this would have -------------------------- and I no no like it

                    providers.Add(new providerInfo { Name = name, Guid = guid });
                }
            }

            return providers;
        }


        private class providerInfo
        {
            public string Name { get; set; }
            public string Guid { get; set; }
        }
        public Form1()
        {
            InitializeComponent();
            loadProvidersIntoListView();
            this.FormBorderStyle = FormBorderStyle.None;
            Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
        }
        private const int CS_DROPSHADOW = 0x20000;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }
        // ts is needed to be able to move the window around GunaUI users dont have to worry about these also stolen from stack overflow
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        private void panel1_Paint(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button5_Click(object sender, EventArgs e)
        {
            var checkedItems = listView1.CheckedItems;
            if (checkedItems.Count == 0)
            {
                MessageBox.Show("Please select a provider.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (ListViewItem item in checkedItems)
            {
                var originalName = item.SubItems[1].Text;
                var guid = item.SubItems[2].Text;
                var invalidChars = Path.GetInvalidFileNameChars();
                var pattern = $"[{Regex.Escape(new string(invalidChars))}]";
                var provName = Regex.Replace(originalName, pattern, "_");
                provName = Regex.Replace(provName, @"[\s:]+", "_"); // we need to follow the format of wpr RAH!
                try
                {
                    var xml = generateWprpProfile(provName, guid);
                    var outputFilePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), $"{provName}.wprp");
                    File.WriteAllText(outputFilePath, xml, Encoding.UTF8);
                    item.Checked = false;
                    MessageBox.Show($"Profile generated successfully: {outputFilePath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to generate profile for {originalName}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }





        private string generateWprpProfile(string providerName, string providerGuid)
        {
            var user = Environment.UserName;
            return $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<WindowsPerformanceRecorder Version=""1.0"" Author=""{user}"" Company="""">
  <Profiles>
    <!-- System session -->
    <SystemCollector Id=""SystemCollector"" Name=""System Collector"">
      <BufferSize Value=""256""/>
      <Buffers Value=""3"" PercentageOfTotalMemory=""true"" MaximumBufferSpace=""256""/>
    </SystemCollector>

    <!-- ETW session for nonpaged-pool providers -->
    <EventCollector Id=""EventCollector-NonpagedPool"" Name=""Event Collector NonpagedPool"">
      <BufferSize Value=""256""/>
      <Buffers Value=""3"" PercentageOfTotalMemory=""true"" MaximumBufferSpace=""64""/>
    </EventCollector>

    <!-- Kernel events (optional) -->
    <SystemProvider Id=""SystemProvider"">
      <Keywords>
        <Keyword Value=""Loader""/>
        <Keyword Value=""ProcessThread""/>
      </Keywords>
    </SystemProvider>

    <!-- Custom provider -->
    <EventProvider
      Id=""Manifested/{providerName}""
      Name=""{providerGuid}""
      Level=""5""
      NonPagedMemory=""true""
      Stack=""true""/>

    <!-- Verbose/Memory profile -->
    <Profile
      Id=""{providerName}.Verbose.Memory""
      Name=""{providerName}""
      LoggingMode=""Memory""
      DetailLevel=""Verbose""
      Description=""Capture all {providerName} events in memory"">
      <Collectors>
        <SystemCollectorId Value=""SystemCollector"">
          <SystemProviderId Value=""SystemProvider""/>
        </SystemCollectorId>
        <EventCollectorId Value=""EventCollector-NonpagedPool"">
          <EventProviders>
            <EventProviderId Value=""Manifested/{providerName}""/>
          </EventProviders>
        </EventCollectorId>
      </Collectors>
    </Profile>
  </Profiles>
</WindowsPerformanceRecorder>";
        }
        // this is literally just a search bar I just wanted to add a code comment here thats it thank you! 
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            string searchText = searchBox.Text.ToLower();

            listView1.Items.Clear();

            foreach (var item in allListViewItems)
            {
                string itemName = item.SubItems[1].Text.ToLower();

                if (itemName.Contains(searchText))
                {
                    listView1.Items.Add(item);
                }
            }
        }
    }
}

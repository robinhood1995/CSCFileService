using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.SqlServer;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Contexts;
using System.Security.Cryptography;
using System.Security.Policy;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.Web.Configuration;
using System.Web.Routing;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;
using Microsoft.VisualBasic.Logging;
using static System.Net.Mime.MediaTypeNames;

//dotnet tool install --global dotnet-ef
//dotnet ef migrations add CreateInitial
//dotnet ef database update

namespace CSCFileService

{

    public partial class Service : ServiceBase
    {

        public int intPurge = Properties.Settings.Default.PurgeFilesDays;
        public string strStorageFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        public string strWorkFolder = "";
        public string strReportFolder ="";
        public string strLogFolder = "";

        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        Timer timer = new Timer(); // name space(using System.Timers;)  
        public Service()
        {
            InitializeComponent();
            log4net.Config.BasicConfigurator.Configure();
            _log.Info("Inititalizing Service");
        }

        public void OnDebug()
        {
            OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            _log.Info("Starting the Application");
            timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
            if (Properties.Settings.Default.CycleTimeMinutes < 3)
            {
#if DEBUG
                _log.Debug($"Cycle time is set to {Properties.Settings.Default.CycleTimeMinutes} min");
                timer.Interval = Properties.Settings.Default.CycleTimeMinutes * 60000; //number in milliseconds  
#else
                _log.Info("Cycle time is set less then 3 minutes so we ae defaulting to 3 min");
                timer.Interval = 3 * 60000; //number in milliseconds  
#endif

            }
            else
            {
                _log.Debug($"Cycle time is set to {Properties.Settings.Default.CycleTimeMinutes} min");
                timer.Interval = Properties.Settings.Default.CycleTimeMinutes * 60000; //number in milliseconds  
            }
            timer.Enabled = true;
            CheckDirectories();
            PurgeFiles();
        }

        protected override void OnStop()
        {
            _log.Info("Service is stopped");
        }
        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            _log.Info("Service is recall");
            _log.Info("Reloading the parameters from the config file incase there were changes");
            Properties.Settings.Default.Reload();

            if (DateTime.Now.Hour == 0 && DateTime.Now.Minute < 15)
            {
                _log.Info("Running nightly purge between 00:00 to 00:15");
                CheckDirectories();
                PurgeFiles();
            }
            ProcessFilesDB();
            //ChangeFiles();
        }

#region CheckDirectories
        public void CheckDirectories()
        {
            _log.Info("Checking directories exist");
            strWorkFolder = strStorageFolder + @"\CSCFileService";
            strReportFolder = strWorkFolder + @"\Report";
            strLogFolder = strWorkFolder + @"\Log";

            _log.Debug("Checking if the folders exist");
            if (!Directory.Exists(Properties.Settings.Default.PickUpFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.PickUpFolder);
                _log.Debug("Created " + Properties.Settings.Default.PickUpFolder + " folder");
            }
            if (!Directory.Exists(Properties.Settings.Default.ArchiveFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.ArchiveFolder);
                _log.Debug("Created " + Properties.Settings.Default.ArchiveFolder + " folder");
            }
            if (!Directory.Exists(strReportFolder))
            {
                Directory.CreateDirectory(strReportFolder);
                _log.Debug("Created " + strReportFolder + " folder");
            }
            if (!Directory.Exists(strLogFolder))
            {
                Directory.CreateDirectory(strLogFolder);
                _log.Debug("Created " + strLogFolder + " folder");
            }
            if (!Directory.Exists(Properties.Settings.Default.WorkFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.WorkFolder);
                _log.Debug("Created " + Properties.Settings.Default.WorkFolder + " folder");
            }
            if (!Directory.Exists(Properties.Settings.Default.OutPutFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.OutPutFolder);
                _log.Debug("Created " + Properties.Settings.Default.OutPutFolder + " folder");
            }
            _log.Debug("Folders verified");
            _log.Info("Finished checking directories exist");
        }
#endregion

#region PurgeFiles
        public void PurgeFiles()
        {
            _log.Info("Purging files");
            strWorkFolder = strStorageFolder + @"\CSCFileService";
            strLogFolder = strWorkFolder + @"\Log";
            string[] logFiles = Directory.GetFiles(strLogFolder);
            foreach (string logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);

                if (fileInfo.CreationTime < DateTime.Now.AddDays(intPurge * -1))
                {
                    _log.Debug("Found Log file(s) that are more then " + intPurge + " days old to delete " + fileInfo.ToString());

                    _log.Debug("Verifying that the old file ends with .log");
                    if (fileInfo.Name.Contains(".log"))
                    {
                        fileInfo.Delete();
                        _log.Debug("Deleted the file " + fileInfo.Name.ToString());
                    }
                    else
                    {
                        _log.Debug("Did not delete the file " + fileInfo.Name.ToString());
                    }
                }
                else
                {
                    _log.Debug("This file" + fileInfo.Name.ToString() + " is not older then " + intPurge + " days");
                }

            }

            //Purging
            strReportFolder = strWorkFolder + @"\Report";
            string[] ReportFiles = Directory.GetFiles(strReportFolder);

            foreach (string ReportFile in ReportFiles)
            {
                var fileInfo = new FileInfo(ReportFile);

                if (fileInfo.CreationTime < DateTime.Now.AddDays(intPurge * -1))
                {
                    _log.Debug("Found Log file(s) that are more then " + intPurge + " days old to delete " + fileInfo.ToString());

                    _log.Debug("Verifying that the old file ends with .csv");
                    if (fileInfo.Name.Contains(".csv"))
                    {
                        fileInfo.Delete();
                        _log.Debug("Deleted the file " + fileInfo.Name.ToString());
                    }
                    else
                    {
                        _log.Debug("Did not delete the file " + fileInfo.Name.ToString());
                    }
                }
                else
                {
                    _log.Debug("This file" + fileInfo.Name.ToString() + " is not older then " + intPurge + " days");
                }

            }
            _log.Info("Finished purging files");
        }
#endregion

#region GetOldestFile(DirectoryInfo directory)
        /// <summary>
        /// Get the oldest files in the directory
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static FileInfo GetOldestFile(DirectoryInfo directory)
        {
            return directory.GetFiles()
                .Union(directory.GetDirectories().Select(d => GetOldestFile(d)))
                .OrderByDescending(f => (f == null ? DateTime.MinValue : f.LastWriteTime))
                .FirstOrDefault();
        }
#endregion

#region GetOldestWriteTimeFromFileInDirectory(DirectoryInfo directoryInfo)
        /// <summary>
        /// Gets the oldest written to in the directory
        /// </summary>
        /// <param name="directoryInfo">Path of the directory that needs to be scanned</param>
        /// <returns></returns>
        private static DateTime GetOldestWriteTimeFromFileInDirectory(DirectoryInfo directoryInfo)
        {
            if (directoryInfo == null || !directoryInfo.Exists)
                return DateTime.MaxValue;

            FileInfo[] files = directoryInfo.GetFiles();
            DateTime lastWrite = DateTime.MaxValue;

            foreach (FileInfo file in files)
            {
                if (file.LastWriteTime > lastWrite)
                {
                    lastWrite = file.LastWriteTime;
                }
            }

            return lastWrite;
        }
#endregion

#region GetOldestFile(DirectoryInfo directoryInfo)
        /// <summary>
        /// Gets the oldest files in the directory
        /// </summary>
        /// <param name="directoryInfo">Path of the directory that needs to be scanned</param>
        /// <returns></returns>
        private static FileInfo GetOldestWritenFileFileInDirectory(DirectoryInfo directoryInfo)
        {
            if (directoryInfo == null || !directoryInfo.Exists)
                return null;

            FileInfo[] files = directoryInfo.GetFiles();
            DateTime lastWrite = DateTime.MaxValue;
            FileInfo lastWritenFile = null;

            foreach (FileInfo file in files)
            {
                if (file.LastWriteTime > lastWrite)
                {
                    lastWrite = file.LastWriteTime;
                    lastWritenFile = file;
                }
            }
            return lastWritenFile;
        }
#endregion

#region ProcessFilesDB
        public void ProcessFilesDB()
        {
            _log.Info("Start processing files to store in the database");
            //https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/how-to-split-a-file-into-many-files-by-using-groups-linq
            //

            //@@IDENTITY of insert of the A,X,Z records
            int idAInserted = 0;
            int idXInserted = 0;
            int idZInserted = 0;

            //Looking for files in the pickup directory
            _log.Debug("Starting to look for files");
            if (System.IO.Directory.Exists(Properties.Settings.Default.PickUpFolder))
            {
                var PickUpDirPath = Properties.Settings.Default.PickUpFolder;
                _log.Debug("Looking in " + PickUpDirPath + " pickup folder");

                var PickUp_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.PickUpFolder);
                _log.Debug("Getting directory infomation from: " + PickUp_Dir);

                var ArchiveDirPath = Properties.Settings.Default.ArchiveFolder;
                _log.Debug("Setting the " + ArchiveDirPath + " archive folder");

                var Archive_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.ArchiveFolder);
                _log.Debug("Getting directory infomation from: " + Archive_Dir);

                var ReportDirPath = strReportFolder;
                _log.Debug("Setting the " + ReportDirPath + " out bound folder");

                var Report_Dir = new System.IO.DirectoryInfo(strReportFolder);
                _log.Debug("Getting directory infomation from: " + Report_Dir);

                var WorkDirPath = Properties.Settings.Default.WorkFolder;
                _log.Debug("Setting the " + WorkDirPath + " out bound folder");

                var Work_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.WorkFolder);
                _log.Debug("Getting directory infomation from: " + Work_Dir);

                var OutDirPath = Properties.Settings.Default.OutPutFolder;
                _log.Debug("Setting the " + OutDirPath + " out bound folder");

                var Out_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.OutPutFolder);
                _log.Debug("Getting directory infomation from: " + Out_Dir);

                //var PickUpFiles = from file in PickUp_Dir.GetFileSystemInfos() select file.Name; //and file.LastWriteTime > DateAdd(DateInterval.Hour, -12, OrderEntryDate) And file.LastWriteTime < DateAdd(DateInterval.Hour, 12, OrderEntryDate) Order By file.LastWriteTime Descending Select file

                //FileInfo[] PickUpFiles = PickUp_Dir.GetFiles(Properties.Settings.Default.FileToLookAt).OrderBy(p => p.CreationTime).ToArray();
                FileInfo[] PickUpFiles = PickUp_Dir.EnumerateFiles(Properties.Settings.Default.FilePatternToLookAt).OrderBy(x => x.CreationTime).Take(Properties.Settings.Default.FilesToImportAtOnce).ToArray();

                if (PickUpFiles.Length > 0)
                {
                    var csv = new StringBuilder();
                    var flf = new StringBuilder();
                    var newLine = "";
                    string fullReportOutput = ReportDirPath  + "\\" + "orderfile" + ".csv";

                    //If a working report file exists rename it and them move it
                    if (File.Exists(fullReportOutput))
                    {
                        File.Move(fullReportOutput, fullReportOutput + "_" + DateTime.Now.ToString("yyyy_MM_dd_HHmm_ss_fff") + ".csv");
                    }

                    foreach (FileInfo filename in PickUpFiles)
                    {
                        _log.Debug("Processing file " + filename);
                        try
                        {

                            string fullPath = PickUpDirPath + "\\" + filename;
                            _log.Debug("Picked Up filename: " + fullPath);

                            //string fullOutput = OutDirPath + "\\" + filename + "_{0}.csv";
                            //string fullOutput = OutDirPath + "\\" + "orderfile" + ".csv";
                            //_log.Debug("Out filename:" + fullOutput);
                            string ArchiveFullPath = ArchiveDirPath + "\\" + filename + "_" + DateTime.Now.ToString("yyyyMMddHHmm_ss_fff") + ".txt";
                            File.Copy(fullPath, ArchiveFullPath);
                            _log.Debug($"Archieved the original file {filename} to the archive folder {ArchiveDirPath}");
                            string WorkFullPath = WorkDirPath + "\\" + filename;
                            if (File.Exists(WorkFullPath))
                            {
                                File.Delete(WorkFullPath);
                                _log.Debug($"Deleted the working file {filename} as it existed in folder {ArchiveDirPath}");
                            }
                            if (File.Exists(WorkFullPath))
                            {
                                File.Delete(WorkFullPath);
                                _log.Debug("Deleted the work file it already exists ");
                                _log.Debug(WorkDirPath);
                            }
                            //File.Copy(fullPath, WorkFullPath);
                            //_log.Debug($"Copied the original file {filename} to the work folder {WorkDirPath}");

                            _log.Debug("Calling the streamreader function");
                            //var reader = File.OpenText(fullPath);
                            using (var fs = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                            {
                                using (var reader = new StreamReader(fs))
                                {
                                    _log.Debug("Calling the textfieldparser function");
                                    // new Parser and Stream for every line
                                    using (var parser = new TextFieldParser(reader))
                                    {

                                        //Add column names before 
                                        _log.Debug("Adding the column headers before we start");
                                        //newLine = "Type,Grade,Flute,Test,DueDate,Width,Length,Quantity,MaxOver,MaxUnder,Boardcode,ShipToName,ShipToAddress1,ShipToAddress2,ShipToCity,ShipToState," +
                                        //    "ShipToZip,ShipToCode,CustomerPO,CustomerPOLine,ClientItem,LoadTag,FirstMachineCode,PiecesPerPallet,FanFold,FanFoldSheetLength,FanFoldUnitHeight," +
                                        //    "FanFoldPerforationLength,CSCOrderID,DoNotUpgrade,TagPerUnit,AdhesiveCode,EOL";
                                        //csv.AppendLine(newLine);
                                        //newLine = "Type,Scores,FILLER,EOL";
                                        //csv.AppendLine(newLine);
                                        //newLine = "Type,SpecialInstructions1,SpecialInstructions2,CustomerOrderNumber,ThreeLRefNo,EOL";
                                        //csv.AppendLine(newLine);
                                        //File.AppendAllText(fullOutput, csv.ToString());
                                        //csv.Clear();
                                        //newLine = "";

                                        //Set parser to Fixed Length Width
                                        parser.TextFieldType = FieldType.FixedWidth;
                                        //parser.HasFieldsEnclosedInQuotes = false;
                                        parser.TrimWhiteSpace = true;

                                        //Setting variables for re-use
                                        string[] currentRowFieldsA;
                                        string[] currentRowFieldsX;
                                        string[] currentRowFieldsZ;
                                        string flute = "";
                                        string width = "";
                                        string grade = "";
                                        string scores = "";
                                        string specialinstructions = "";
                                        string shortbails = "";
                                        string color = "";
                                        while (parser.PeekChars(1) != null)
                                        {

                                            //https://stackoverflow.com/questions/7583770/unable-to-update-the-entityset-because-it-has-a-definingquery-and-no-updatefu
                                            //Parsing row with a A Record 
                                            //Add the row to the .CSV file
                                            //This is to set the lenght of each field in the Fixed Length File
                                            parser.SetFieldWidths(1, 25, 10, 10, 6, 6, 6, 6, 3, 3, 60, 30, 30, 30, 20, 2, 10, 3, 25, 3, 25, 2, 20, 4, 1, 10, 10, 10, 10, 1, 1, 10);
                                            //parser.SetFieldWidths(-1);
                                            //Read all the fields in the row
                                            try 
                                            {
                                                currentRowFieldsA = parser.ReadFields();

                                                if (!(currentRowFieldsA == null))
                                                {
                                                    if (currentRowFieldsA[0].Contains('A'))
                                                    {
                                                        foreach (var field in currentRowFieldsA)
                                                        {
                                                            newLine = newLine + string.Format("{0}", field.Replace(",", "") + ",");
                                                        }
                                                        csv.AppendLine(newLine);
                                                        newLine = "";
                                                    }
                                                }

                                                //Read the line to get the variables
                                                if (!(currentRowFieldsA == null))
                                                {
                                                    if (currentRowFieldsA[0].Contains('A'))
                                                    {
                                                        //Set variables
                                                        flute = currentRowFieldsA[2];
                                                        width = currentRowFieldsA[5];
                                                        grade = currentRowFieldsA[1];
                                                        string EOL = filename.ToString();

                                                        //Adding to the database 
                                                        using (var context = new EDIEntities())
                                                        {
                                                            var ord = new orderfile()
                                                            {
                                                                Type = currentRowFieldsA[0],
                                                                Grade = currentRowFieldsA[1],
                                                                Flute = currentRowFieldsA[2],
                                                                Test = currentRowFieldsA[3],
                                                                DueDate = currentRowFieldsA[4],
                                                                Width = currentRowFieldsA[5],
                                                                Length = currentRowFieldsA[6],
                                                                Quantity = currentRowFieldsA[7],
                                                                MaxOver = currentRowFieldsA[8],
                                                                MaxUnder = currentRowFieldsA[9],
                                                                Boardcode = currentRowFieldsA[10],
                                                                ShipToName = currentRowFieldsA[11],
                                                                ShipToAddress1 = currentRowFieldsA[12],
                                                                ShipToAddress2 = currentRowFieldsA[13],
                                                                ShipToCity = currentRowFieldsA[14],
                                                                ShipToState = currentRowFieldsA[15],
                                                                ShipToZip = currentRowFieldsA[16],
                                                                ShipToCode = currentRowFieldsA[17],
                                                                CustomerPO = currentRowFieldsA[18],
                                                                CustomerPOLine = currentRowFieldsA[19],
                                                                ClientItem = currentRowFieldsA[2] + "|" + currentRowFieldsA[1] + "|" + currentRowFieldsA[5],
                                                                LoadTag = currentRowFieldsA[21],
                                                                FirstMachineCode = currentRowFieldsA[22],
                                                                PiecesPerPallet = currentRowFieldsA[23],
                                                                FanFold = currentRowFieldsA[24],
                                                                FanFoldSheetLength = currentRowFieldsA[25],
                                                                FanFoldUnitHeight = currentRowFieldsA[26],
                                                                FanFoldPerforationLength = currentRowFieldsA[27],
                                                                CSCOrderID = currentRowFieldsA[28],
                                                                DoNotUpgrade = currentRowFieldsA[29],
                                                                TagPerUnit = currentRowFieldsA[30],
                                                                AdhesiveCode = currentRowFieldsA[31],
                                                                EOL = String.Format("{0}", filename),
                                                            };
                                                            context.orderfiles.Add(ord);
                                                            context.SaveChanges();
                                                            idAInserted = ord.ID;
                                                        }

                                                        //Adding the the output file
                                                        using (var context = new EDIEntities())
                                                        {
                                                            var chgFileA = context.orderfiles.Where(s => s.ID == idAInserted).ToList();

                                                            flf.AppendLine(
                                                                chgFileA[0].Type.PadRight(1, ' ') +
                                                                chgFileA[0].Grade.PadRight(25, ' ') +
                                                                chgFileA[0].Flute.PadRight(10, ' ') +
                                                                chgFileA[0].Test.PadRight(10, ' ') +
                                                                chgFileA[0].DueDate.PadRight(6, ' ') +
                                                                chgFileA[0].Width.PadRight(6, ' ') +
                                                                chgFileA[0].Length.PadRight(6, ' ') +
                                                                chgFileA[0].Quantity.PadRight(6, ' ') +
                                                                chgFileA[0].MaxOver.PadRight(3, ' ') +
                                                                chgFileA[0].MaxUnder.PadRight(3, ' ') +
                                                                chgFileA[0].Boardcode.PadRight(60, ' ') +
                                                                chgFileA[0].ShipToName.PadRight(30, ' ') +
                                                                chgFileA[0].ShipToAddress1.PadRight(30, ' ') +
                                                                chgFileA[0].ShipToAddress2.PadRight(30, ' ') +
                                                                chgFileA[0].ShipToCity.PadRight(20, ' ') +
                                                                chgFileA[0].ShipToState.PadRight(2, ' ') +
                                                                chgFileA[0].ShipToZip.PadRight(10, ' ') +
                                                                chgFileA[0].ShipToCode.PadRight(3, ' ') +
                                                                chgFileA[0].CustomerPO.PadRight(25, ' ') +
                                                                chgFileA[0].CustomerPOLine.PadRight(3, ' ') +
                                                                chgFileA[0].ClientItem.PadRight(25, ' ') +
                                                                chgFileA[0].LoadTag.PadRight(2, ' ') +
                                                                chgFileA[0].FirstMachineCode.PadRight(20, ' ') +
                                                                chgFileA[0].PiecesPerPallet.PadRight(4, ' ') +
                                                                chgFileA[0].FanFold.PadRight(1, ' ') +
                                                                chgFileA[0].FanFoldSheetLength.PadRight(10, ' ') +
                                                                chgFileA[0].FanFoldUnitHeight.PadRight(10, ' ') +
                                                                chgFileA[0].FanFoldPerforationLength.PadRight(10, ' ') +
                                                                chgFileA[0].CSCOrderID.PadRight(10, ' ') +
                                                                chgFileA[0].DoNotUpgrade.PadRight(1, ' ') +
                                                                chgFileA[0].TagPerUnit.PadRight(1, ' ') +
                                                                chgFileA[0].AdhesiveCode.PadRight(10, ' ')
                                                                );
                                                        }

                                                    }
                                                }
                                            }
                                            catch (MalformedLineException ex)
                                            {
                                                _log.Error("File " + filename);
                                                _log.Error(ex.Message);
                                                //Emailed as could not process the files
                                                ClsFunctions.NetEmail(Properties.Settings.Default.Emails, "Error File",
                                                                filename.ToString(),
                                                                Properties.Settings.Default.SMTPFROMEMAIL,
                                                                Properties.Settings.Default.SMTPSERVER,
                                                                Int32.Parse(Properties.Settings.Default.SMTPPort),
                                                                Properties.Settings.Default.SMTPUser,
                                                                Properties.Settings.Default.SMTPPassword);
                                            }
                                                                                                
                                               
                                            //Parsing row with a X Record
                                            //Add the row to the .CSV file
                                            //This is to set the lenght of each field in the Fixed Length File
                                            //parser.SetFieldWidths(1, 136, 18);
                                            parser.SetFieldWidths(1, -1);
                                            //Read all the fields in the row
                                            try
                                            { 
                                                currentRowFieldsX = parser.ReadFields();

                                                if (!(currentRowFieldsX == null))
                                                {
                                                    if (currentRowFieldsX[0].Contains('X'))
                                                    {
                                                        foreach (var field in currentRowFieldsX)
                                                        {
                                                            newLine = newLine + string.Format("{0}", field.Replace(",", "") + ",");
                                                        }
                                                        csv.AppendLine(newLine);
                                                        newLine = "";
                                                    }
                                                }

                                                //Read the line to get the variables
                                                if (!(currentRowFieldsX == null))
                                            {
                                                if (currentRowFieldsX[0].Contains('X'))
                                                {
                                                    //Set variables
                                                    scores = currentRowFieldsX[1];

                                                    //Adding to the database 
                                                    using (var context = new EDIEntities())
                                                    {
                                                        var scr = new Score()
                                                        {
                                                            orderfileID = idAInserted,
                                                            Type = currentRowFieldsX[0],
                                                            Scores = currentRowFieldsX[1],
                                                            //FILLER = currentRowFieldsX[2],
                                                            EOL = String.Format("{0}", filename),
                                                        };
                                                        context.Scores.Add(scr);
                                                        context.SaveChanges();
                                                        idXInserted = scr.ID;

                                                        //Add to unique code if there are scores
                                                        if (currentRowFieldsX[1].Length > 0)
                                                        {
                                                            //Look to see if any other orders had it before to re-use the code
                                                            var lookup = (from o in context.orderfiles
                                                                            join s in context.Scores
                                                                            on o.ID equals s.orderfileID
                                                                            select new
                                                                            {
                                                                                ClientItem = o.ClientItem,
                                                                                Flute = o.Flute,
                                                                                Width = o.Width,
                                                                                Grade = o.Grade,
                                                                                ScoresID = s.ID,
                                                                                Scores = s.Scores,
                                                                            })
                                                                            .Where(s => s.Flute == flute &&
                                                                            s.Width == width &&
                                                                            s.Grade == grade &&
                                                                            s.Scores == scores)
                                                                            .ToList();

                                                            var upd = context.orderfiles.Where(o => o.ID == idAInserted).FirstOrDefault();
                                                            if (lookup.Count > 1)
                                                            {
                                                                string chg = upd.ClientItem + "|" + lookup[0].ScoresID;
                                                                flf.Replace(upd.ClientItem.PadRight(25, ' '), chg.PadRight(25, ' '));
                                                                upd.ClientItem = upd.ClientItem + "|" + lookup[0].ScoresID;

                                                                }
                                                            else
                                                            {
                                                                upd.ClientItem = upd.ClientItem + "|" + String.Format("{0}", idXInserted);
                                                            }
                                                            //context.orderfiles.Add(upd);
                                                            context.SaveChanges();
                                                        }
                                                    }

                                                    //Adding to the output file
                                                    using (var context = new EDIEntities())
                                                    {
                                                        var chgFileX = context.Scores.Where(s => s.orderfileID == idAInserted).ToList();

                                                        flf.AppendLine(
                                                            chgFileX[0].Type.PadRight(1, ' ') +
                                                            chgFileX[0].Scores.PadRight(136, ' ') +
                                                            " ".PadRight(18, ' ')
                                                            );
                                                    }
                                                }
                                            }
                                            }
                                            catch (MalformedLineException ex)
                                            {
                                                _log.Error("File " + filename);
                                                _log.Error(ex.Message);
                                                //Adding the error to the database
                                                using (var context = new EDIEntities())
                                                {
                                                    var scr = new Score()
                                                    {
                                                        orderfileID = idAInserted,
                                                        Type = "X",
                                                        Scores = "Error in file structure",
                                                        FILLER = "",
                                                        EOL = String.Format("{0}", filename),
                                                    };
                                                    context.Scores.Add(scr);
                                                    context.SaveChanges();
                                                }
                                                //Adding the error to the file
                                                flf.AppendLine("X" +
                                                "Error in file structure".PadRight(136, ' ') +
                                                " ".PadRight(18, ' ')
                                                );
                                            }
                                                                                           

                                            //Parsing row with a Z Record
                                            //Add the row to the .CSV file
                                            //This is to set the lenght of each field in the Fixed Length File
                                            parser.SetFieldWidths(1, 510, 510, 50, 50);
                                            //Read all the fields in the row
                                            try 
                                            { 
                                                currentRowFieldsZ = parser.ReadFields();

                                                if (!(currentRowFieldsZ == null))
                                                {
                                                    if (currentRowFieldsZ[0].Contains('Z'))
                                                    {
                                                        foreach (var field in currentRowFieldsZ)
                                                        {
                                                            newLine = newLine + string.Format("{0}", field.Replace(",", "") + ",");
                                                        }
                                                        csv.AppendLine(newLine);
                                                        newLine = "";
                                                    }
                                                }

                                                //Read the line to get the variables
                                                if (!(currentRowFieldsZ == null))
                                                {
                                                    if (currentRowFieldsZ[0].Contains('Z'))
                                                    {
                                                        //Set variables
                                                        specialinstructions = currentRowFieldsZ[1];
                                                        color = currentRowFieldsZ[2];

                                                        //Adding to the database 
                                                        using (var context = new EDIEntities())
                                                        {
                                                            var sp = new SpecialInstruction()
                                                            {
                                                                orderfileID = idAInserted,
                                                                Type = currentRowFieldsZ[0],
                                                                SpecialInstruction1 = currentRowFieldsZ[1],
                                                                SpecialInstruction2 = currentRowFieldsZ[2],
                                                                CustomerOrderNumber = currentRowFieldsZ[3],
                                                                ThreePLRefNo = currentRowFieldsZ[4],
                                                                EOL = String.Format("{0}", filename),
                                                            };
                                                            context.SpecialInstructions.Add(sp);
                                                            context.SaveChanges();
                                                            idZInserted = sp.ID;

                                                            //Add to unique code if there are suposed to be short bails
                                                            if (currentRowFieldsZ[1].ToLower().Contains("short"))
                                                            {
                                                                shortbails = currentRowFieldsZ[1];
                                                                var upd = context.orderfiles.Where(o => o.ID == idAInserted).FirstOrDefault();
                                                                string chg = upd.ClientItem + "|SB";
                                                                flf.Replace(upd.ClientItem.PadRight(25, ' '), chg.PadRight(25, ' '));
                                                                upd.ClientItem = upd.ClientItem + "|SB";
                                                                //context.orderfiles.Add(upd);
                                                                context.SaveChanges();
                                                            }

                                                            //Add to unique code if there is colour
                                                            if (currentRowFieldsZ[2].ToLower().Contains("color"))
                                                            {
                                                                color = currentRowFieldsZ[2].Split(',').ToString();
                                                                var upd = context.orderfiles.Where(o => o.ID == idAInserted).FirstOrDefault();
                                                                string chg = upd.ClientItem + "|" + String.Format("{0}", color);
                                                                flf.Replace(upd.ClientItem.PadRight(25, ' '), chg.PadRight(25, ' '));
                                                                upd.ClientItem = upd.ClientItem + "|" + String.Format("{0}", color);
                                                                //context.orderfiles.Add(upd);
                                                                context.SaveChanges();
                                                            }
                                                        }

                                                        //Adding to the output file
                                                        using (var context = new EDIEntities())
                                                        {
                                                            var chgFileZ = context.SpecialInstructions.Where(s => s.orderfileID == idAInserted).ToList();

                                                            flf.AppendLine(
                                                                chgFileZ[0].Type.PadRight(1, ' ') +
                                                                chgFileZ[0].SpecialInstruction1.PadRight(510, ' ') +
                                                                chgFileZ[0].SpecialInstruction2.PadRight(510, ' ') +
                                                                chgFileZ[0].CustomerOrderNumber.PadRight(50, ' ') +
                                                                chgFileZ[0].ThreePLRefNo.PadRight(50, ' ')
                                                                );
                                                        }
                                                    }
                                                }
                                            }
                                            catch (MalformedLineException ex)
                                            {
                                                _log.Error("Malformed file " + filename);
                                                _log.Error(ex.Message);
                                                //Adding the error to the database
                                                using (var context = new EDIEntities())
                                                {
                                                    var sp = new SpecialInstruction()
                                                    {
                                                        orderfileID = idAInserted,
                                                        Type = "Z",
                                                        SpecialInstruction1 = "Error in file structure",
                                                        SpecialInstruction2 = "",
                                                        CustomerOrderNumber = "",
                                                        ThreePLRefNo = "",
                                                        EOL = String.Format("{0}", filename),
                                                    };
                                                    context.SpecialInstructions.Add(sp);
                                                    context.SaveChanges();
                                                    idZInserted = sp.ID;
                                                }
                                                //Adding the error to the file
                                                flf.AppendLine("Z" +
                                                        "Error in file structure".PadRight(510, ' ') +
                                                        "".PadRight(510, ' ') +
                                                        "".PadRight(50, ' ') +
                                                        "".PadRight(50, ' ')
                                                        );
                                            }

                                            string outFile = Out_Dir + "\\" + filename + "_" + DateTime.Now.ToString("yyyy_MM_dd_HHmm_ss_fff") + ".txt";
                                            File.AppendAllText(outFile , flf.ToString());
                                            _log.Debug($"Created the import file {outFile}");
                                            flf.Clear();
                                            _log.Debug("Closed the stringbuilder");
                                                
                                            File.AppendAllText(fullReportOutput, csv.ToString());
                                            _log.Debug($"Created the file {fullReportOutput}");
                                            string NewFile = fullReportOutput + "_" + DateTime.Now.ToString("yyyyMMddHHmm_ss_fff") + ".csv";
                                            File.Move(fullReportOutput, NewFile);
                                            _log.Debug($"Moved the {fullReportOutput} file {NewFile}");
                                            csv.Clear();
                                            _log.Debug("Closed the stringbuilder");
                                            if (Properties.Settings.Default.EmailCopy)
                                            {
                                                //Send email of file
                                                ClsFunctions.NetEmail(Properties.Settings.Default.Emails, "CSC File",
                                                            NewFile, Properties.Settings.Default.SMTPFROMEMAIL,
                                                            Properties.Settings.Default.SMTPSERVER,
                                                            Int32.Parse(Properties.Settings.Default.SMTPPort),
                                                            Properties.Settings.Default.SMTPUser,
                                                            Properties.Settings.Default.SMTPPassword);
                                                _log.Debug($"Emailed file {NewFile} copy to {Properties.Settings.Default.Emails}");
                                            }

                                        }
                                        parser.Close();
                                        _log.Debug($"Closed the parser reading {filename}");
                                    }
                                    reader.Close();
                                    _log.Debug($"Closed the reader reading {filename}");
                                }
                                fs.Close();
                                _log.Debug($"Closed the file stream reading {filename}");
                            }
                            File.Delete(PickUpDirPath + "\\" + filename);
                            _log.Debug($"Deleting the original file named {filename}");
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex.Message);
                            _log.Error("File " + filename + " has an issue");
                        }

                    }

                }
                else
                {
                    _log.Debug("No files to process");
                    return;
                }
            }
            else
            {
                _log.Debug("PickUp directory does not exist");
            }

            _log.Info("Finished processing files to store in the database");
            base.Dispose();
            Dispose(true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
#endregion
    
    }

}

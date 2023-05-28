using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.SqlServer;
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
using Microsoft.VisualBasic.FileIO;

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
            ChangeFiles();
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
                            File.Copy(fullPath, WorkFullPath);
                            _log.Debug($"Copied the original file {filename} to the work folder {ArchiveDirPath}");

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
                                            try
                                            {

                                                //https://stackoverflow.com/questions/7583770/unable-to-update-the-entityset-because-it-has-a-definingquery-and-no-updatefu
                                                //Parsing row with a A Record 
                                                //Add the row to the .CSV file
                                                //This is to set the lenght of each field in the Fixed Length File
                                                parser.SetFieldWidths(1, 25, 10, 10, 6, 6, 6, 6, 3, 3, 60, 30, 30, 30, 20, 2, 10, 3, 25, 3, 25, 2, 20, 4, 1, 10, 10, 10, 10, 1, 1, 10);
                                                //parser.SetFieldWidths(-1);
                                                //Read all the fields in the row
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
                                                        try
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
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            _log.Error("File " + filename);
                                                            _log.Error(ex.Message);
                                                        }
                                                    }
                                                }
                                                //Parsing row with a X Record
                                                //Add the row to the .CSV file
                                                //This is to set the lenght of each field in the Fixed Length File
                                                parser.SetFieldWidths(1, 136, 18);
                                                //Read all the fields in the row
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
                                                        try
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
                                                                    //FILLER = currentRowFields[2],
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
                                                                                  on o.ID equals s.orderfileID into r1
                                                                                  from s in r1.DefaultIfEmpty()
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
                                                                    if (lookup.Count>1)
                                                                    {
                                                                        upd.ClientItem = upd.ClientItem + "|" + String.Format("{0}", lookup[0].ClientItem);
                                                                    }
                                                                    else
                                                                    {
                                                                        upd.ClientItem = upd.ClientItem + "|" + String.Format("{0}", idXInserted);
                                                                    }
                                                                    //context.orderfiles.Add(upd);
                                                                    context.SaveChanges();
                                                                }
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            _log.Error("File " + filename);
                                                            _log.Error(ex.Message);
                                                        }
                                                    }
                                                }

                                                //Parsing row with a Z Record
                                                //Add the row to the .CSV file
                                                //This is to set the lenght of each field in the Fixed Length File
                                                parser.SetFieldWidths(1, 510, 510, 50, 50);
                                                //Read all the fields in the row
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
                                                        try
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
                                                                    upd.ClientItem = upd.ClientItem + "|SB";
                                                                    //context.orderfiles.Add(upd);
                                                                    context.SaveChanges();
                                                                }

                                                                //Add to unique code if there is colour
                                                                if (currentRowFieldsZ[2].ToLower().Contains("color"))
                                                                {
                                                                    color = currentRowFieldsZ[2].Split(',').ToString();
                                                                    var upd = context.orderfiles.Where(o => o.ID == idAInserted).FirstOrDefault();
                                                                    upd.ClientItem = upd.ClientItem + "|" + String.Format("{0}", color);
                                                                    //context.orderfiles.Add(upd);
                                                                    context.SaveChanges();
                                                                }
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            _log.Error("File " + filename);
                                                            _log.Error(ex.Message);
                                                        }
                                                    }
                                                }

                                                ////Checking if the order exists with a unique code to be able to update this record for re-use in Kiwiplan ESP
                                                //using (var context = new EDIEntities())
                                                //{

                                                //    var eChk = (from o in context.orderfiles
                                                //                join s in context.Scores
                                                //                on o.ID equals s.orderfileID into r1
                                                //                from s in r1.DefaultIfEmpty()
                                                //                join si1 in context.SpecialInstructions
                                                //                on o.ID equals si1.orderfileID into r2
                                                //                from si1 in r2.DefaultIfEmpty()
                                                //                join si2 in context.SpecialInstructions
                                                //                on o.ID equals si2.orderfileID into r3
                                                //                from si2 in r3.DefaultIfEmpty()
                                                //                select new
                                                //                {
                                                //                    ClientItem = o.ClientItem,
                                                //                    Flute = o.Flute,
                                                //                    Width = o.Width,
                                                //                    Grade = o.Grade,
                                                //                    ScoresID = s.ID,
                                                //                    Scores = s.Scores,
                                                //                    SpecialInstruction1ID = si1.ID,
                                                //                    SpecialInstruction1 = si1.SpecialInstruction1,
                                                //                    SpecialInstruction2ID = si2.ID,
                                                //                    SpecialInstruction2 = si2.SpecialInstruction2,

                                                //                })
                                                //             .Where(s => 
                                                //             s.Flute == flute && 
                                                //             s.Width == width && 
                                                //             s.Grade == grade
                                                //             ).AsQueryable();

                                                //Requery the result set from above
                                                //    if (scores.Length>0)
                                                //    {
                                                //        eChk = eChk.Where(s => s.Scores == scores);
                                                //    }

                                                //}
                                                    
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
                                            catch (Exception ex)
                                            {
                                                _log.Error("File " + filename);
                                                _log.Error(ex.Message);
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

#region ChangeFiles
        public void ChangeFiles()
        {
            _log.Info("Start processing files to change the files for Kiwiplan");

            FileInfo[] PickUpFiles = new System.IO.DirectoryInfo(Properties.Settings.Default.WorkFolder).GetFiles(Properties.Settings.Default.FilePatternToLookAt).OrderBy(p => p.CreationTime).ToArray();

            if (PickUpFiles.Length > 0)
            {
                //Create a container to rebuild the file with the new field
                var sbText = new StringBuilder();

                foreach (FileInfo filename in PickUpFiles)
                {

                    string fullPath = Properties.Settings.Default.WorkFolder + "\\" + filename;
                    _log.Debug("Picked Up filename: " + fullPath);

                    var OutDirPath = Properties.Settings.Default.OutPutFolder;
                    _log.Debug("Setting the " + OutDirPath + " out bound folder");

                    var Out_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.OutPutFolder);
                    _log.Debug("Getting directory infomation from: " + Out_Dir);

                    int ordCount = 0;

                    using (var fs = new FileStream(fullPath, FileMode.OpenOrCreate , FileAccess.ReadWrite, FileShare.ReadWrite))
                    {
                        _log.Debug("Calling the streamreader function");
                        //var reader = File.OpenText(fullPath);
                        using (var reader = new StreamReader(fs))
                        {
                            _log.Debug("Calling the textfieldparser function");
                            // new Parser and Stream for every line
                            using (var parser = new TextFieldParser(reader))
                            {
                                var newLine = "";
                                //Set parser to Fixed Length Width
                                parser.HasFieldsEnclosedInQuotes = false;
                                parser.TextFieldType = FieldType.FixedWidth;
                                string[] currentRowFields;
                                while (parser.PeekChars(1) != null)
                                {
                                    try
                                    {
                                        //Parsing row with a A Record 
                                        // This is to set the lenght of each field in the Fixed Length File
                                        parser.SetFieldWidths(1, 25, 10, 10, 6, 6, 231, 25, -1);
                                        //Read all the fields in the row
                                        currentRowFields = parser.ReadFields();
                                        if (!(currentRowFields == null))
                                        {
                                            if (currentRowFields[0].Contains('A'))
                                            {
                                                var f = currentRowFields[2];
                                                var w = currentRowFields[5];
                                                var g = currentRowFields[1];
                                                                                                
                                                //Get unique code from EDI database
                                                using (var er = new EDIEntities())
                                                {

                                                    var err = (from o in er.orderfiles
                                                               join s in er.Scores
                                                               on o.ID equals s.orderfileID into r1
                                                               from r in r1.DefaultIfEmpty()
                                                               select new
                                                               {
                                                                   ClientItem = o.ClientItem,
                                                                   Flute = o.Flute,
                                                                   Width = o.Width,
                                                                   Grade = o.Grade,
                                                               })
                                                               .Where(s => s.Flute == f && s.Width == w && s.Grade == g)
                                                               .ToList();

                                                    //var err = er.orderfiles.J(er.Scores, of => of.ID,
                                                    //                            si => si.ID, (of, si) => new
                                                    //                            {
                                                    //                               ID = of.ID,
                                                    //                               Flute = of.Flute,
                                                    //                               Width = of.Width,
                                                    //                               Grade = of.Grade,
                                                    //                               ClientItem = of.ClientItem 
                                                    //                            })
                                                    //                        .Where(s => s.Flute == f && s.Width == w && s.Grade == g)
                                                    //                        .Select(s => new orderfile()
                                                    //                                {
                                                    //                                    ClientItem = s.ClientItem
                                                    //                                })
                                                    //                        .ToList();
                                                    //var err = er.orderfiles.SqlQuery("select o.ClientItem" +
                                                    //"from orderfile o " +
                                                    //"LEFT Join score s " +
                                                    //"ON o.id = s.orderfileID " +
                                                    //"LEFT Join SpecialInstruction sp" +
                                                    //"ON o.id = sp.orderfileID" +
                                                    //"where o.ClientItem like '" + f + "|" + g + "|" + w + "%'" +
                                                    //"and s.Scores = s.Scores").ToList();

                                                    //rText = err;
                                                    currentRowFields[7] = err[0].ClientItem.PadRight(25).ToString();
                                                }

                                                newLine = currentRowFields[0]
                                                    + currentRowFields[1].PadRight(25).ToString()
                                                    + currentRowFields[2].PadRight(10).ToString()
                                                    + currentRowFields[3].PadRight(10).ToString()
                                                    + currentRowFields[4].PadRight(6).ToString()
                                                    + currentRowFields[5].PadRight(6).ToString()
                                                    + currentRowFields[6].PadRight(231).ToString()
                                                    + currentRowFields[7]
                                                    + currentRowFields[8].PadRight(81).ToString();
                                                sbText.AppendLine(newLine);
                                                newLine = "";


                                            }

                                            //Parsing row with a X Record
                                            parser.SetFieldWidths(-1);
                                            currentRowFields = parser.ReadFields();
                                            if (!(currentRowFields == null))
                                            {
                                                if (currentRowFields[0].Contains('X'))
                                                {
                                                    foreach (var field in currentRowFields)
                                                    {
                                                        newLine = newLine + string.Format("{0}", field);
                                                    }
                                                    sbText.AppendLine(newLine);
                                                    newLine = "";
                                                }
                                            }

                                            //Parsing row with a Z Record
                                            parser.SetFieldWidths(-1);
                                            currentRowFields = parser.ReadFields();
                                            if (!(currentRowFields == null))
                                            {
                                                if (currentRowFields[0].Contains('Z'))
                                                {
                                                    foreach (var field in currentRowFields)
                                                    {
                                                        newLine = newLine + string.Format("{0}", field.Replace(",", "") + ",");
                                                    }
                                                    sbText.AppendLine(newLine);
                                                    newLine = "";
                                                }
                                            }

                                        }

                                        ordCount++;
                                        if (ordCount == 1)
                                        {
                                            File.AppendAllText(Out_Dir + "\\" + filename + "_" + DateTime.Now.ToString("yyyy_MM_dd_HHmm_ss_fff") + ".txt", sbText.ToString());
                                            _log.Debug("Created the file");
                                            sbText.Clear();
                                            _log.Debug("Closed the stringbuilder");
                                            ordCount = 0;
                                        }


                                    }
                                    catch (Exception ex)
                                    {
                                        _log.Error(ex.Message);
                                        _log.Error("File " + filename + " has an issue");
                                        sbText.Clear();
                                        _log.Error("Closed the stringbuilder");
                                        continue;
                                    }
                                    finally
                                    {

                                    }

                                }
                                parser.Close();
                                _log.Debug("Closed the parser");
                            }
                            reader.Close();
                            _log.Debug("Closed the reader");
                        }
                        fs.Close();
                        fs.Close();
                        _log.Debug("Closed the file stream");
                    }
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        _log.Debug("Deleting working file");
                    }
                }

            }
            _log.Info("Finished processing files to change the files for Kiwiplan");
            base.Dispose();
            Dispose(true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
#endregion
    
    }

}

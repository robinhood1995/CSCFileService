using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using Microsoft.VisualBasic.FileIO;

//dotnet tool install --global dotnet-ef
//dotnet ef migrations add CreateInitial
//dotnet ef database update

namespace CSCFileService

{

    public partial class Service : ServiceBase
    {

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
            timer.Interval = Properties.Settings.Default.CycleTimeMinutes * 60000; //number in milliseconds  
            timer.Enabled = true;
        }

        protected override void OnStop()
        {
            _log.Info("Service is stopped");
        }
        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            _log.Info("Service is recall");
            CheckDirectories();
            ProcessFilesDB();
            ChangeFiles();
        }

        #region CheckDirectories()
        public void CheckDirectories()
        {
            _log.Info("Checking if the folders exist");
            if (!Directory.Exists(Properties.Settings.Default.PickUpFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.PickUpFolder);
                _log.Info("Created " + Properties.Settings.Default.PickUpFolder + " folder");
            }
            if (!Directory.Exists(Properties.Settings.Default.ArchiveFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.ArchiveFolder);
                _log.Info("Created " + Properties.Settings.Default.ArchiveFolder + " folder");
            }
            if (!Directory.Exists(Properties.Settings.Default.ReportFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.ReportFolder);
                _log.Info("Created " + Properties.Settings.Default.ReportFolder + " folder");
            }
            if (!Directory.Exists(Properties.Settings.Default.WorkFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.WorkFolder);
                _log.Info("Created " + Properties.Settings.Default.WorkFolder + " folder");
            }
            if (!Directory.Exists(Properties.Settings.Default.OutPutFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.OutPutFolder);
                _log.Info("Created " + Properties.Settings.Default.OutPutFolder + " folder");
            }
            _log.Info("Folders verified");
        }
        #endregion 

        #region GetOldestFile(DirectoryInfo directory)
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
        /// Returns oldest writen file from the specified directory.
        /// If the directory does not exist or doesn't contain any file, DateTime.MinValue is returned.
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
        /// Returns file's oldest writen timestamp from the specified directory.
        /// If the directory does not exist or doesn't contain any file, null is returned.
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
            //https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/how-to-split-a-file-into-many-files-by-using-groups-linq
            //

            //@@IDENTITY of insert of the A record
            int idInserted = 0;

            //Looking for files in the pickup directory
            _log.Info("Starting to look for files");
            if (System.IO.Directory.Exists(Properties.Settings.Default.PickUpFolder))
            {
                var PickUpDirPath = Properties.Settings.Default.PickUpFolder;
                _log.Info("Looking in " + PickUpDirPath + " pickup folder");

                var PickUp_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.PickUpFolder);
                _log.Info("Getting directory infomation from: " + PickUp_Dir);

                var ArchiveDirPath = Properties.Settings.Default.ArchiveFolder;
                _log.Info("Setting the " + ArchiveDirPath + " archive folder");

                var Archive_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.ArchiveFolder);
                _log.Info("Getting directory infomation from: " + Archive_Dir);

                var ReportDirPath = Properties.Settings.Default.ReportFolder;
                _log.Info("Setting the " + ReportDirPath + " out bound folder");

                var Report_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.ReportFolder);
                _log.Info("Getting directory infomation from: " + Report_Dir);

                var WorkDirPath = Properties.Settings.Default.WorkFolder;
                _log.Info("Setting the " + WorkDirPath + " out bound folder");

                var Work_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.WorkFolder);
                _log.Info("Getting directory infomation from: " + Work_Dir);

                var OutDirPath = Properties.Settings.Default.OutPutFolder;
                _log.Info("Setting the " + OutDirPath + " out bound folder");

                var Out_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.OutPutFolder);
                _log.Info("Getting directory infomation from: " + Out_Dir);

                //var PickUpFiles = from file in PickUp_Dir.GetFileSystemInfos() select file.Name; //and file.LastWriteTime > DateAdd(DateInterval.Hour, -12, OrderEntryDate) And file.LastWriteTime < DateAdd(DateInterval.Hour, 12, OrderEntryDate) Order By file.LastWriteTime Descending Select file

                //FileInfo[] PickUpFiles = PickUp_Dir.GetFiles(Properties.Settings.Default.FileToLookAt).OrderBy(p => p.CreationTime).ToArray();
                FileInfo[] PickUpFiles = PickUp_Dir.EnumerateFiles(Properties.Settings.Default.FileToLookAt).OrderBy(x => x.CreationTime).Take(Properties.Settings.Default.FilesToImportAtOnce).ToArray();

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
                        _log.Info("Processing file " + filename);
                        try
                        {

                            string fullPath = PickUpDirPath + "\\" + filename;
                            _log.Info("Picked Up filename: " + fullPath);

                            //string fullOutput = OutDirPath + "\\" + filename + "_{0}.csv";
                            //string fullOutput = OutDirPath + "\\" + "orderfile" + ".csv";
                            //_log.Info("Out filename:" + fullOutput);
                            string ArchiveFullPath = ArchiveDirPath + "\\" + filename + "_" + DateTime.Now.ToString("yyyy_MM_dd_HHmm_ss_fff") + ".txt";
                            File.Copy(fullPath, ArchiveFullPath);
                            _log.Info($"Archieved the original file {filename} to the archive folder {ArchiveDirPath}");
                            string WorkFullPath = WorkDirPath + "\\" + filename;
                            if (File.Exists(WorkFullPath))
                            {
                                File.Delete(WorkFullPath);
                                _log.Info($"Deleted the working file {filename} as it existed in folder {ArchiveDirPath}");
                            }
                            File.Copy(fullPath, WorkFullPath);
                            _log.Info($"Copied the original file {filename} to the work folder {ArchiveDirPath}");

                            _log.Info("Calling the streamreader function");
                            //var reader = File.OpenText(fullPath);
                            using (var reader = new StreamReader(fullPath))
                            {
                                //var row = reader.ReadToEnd;

                                _log.Info("Calling the textfieldparser function");
                                // new Parser and Stream for every line
                                using (var parser = new TextFieldParser(reader))
                                {

                                    //Add column names before 
                                    _log.Info("Adding the column headers before we start");
                                    newLine = "Type,Grade,Flute,Test,DueDate,Width,Length,Quantity,MaxOver,MaxUnder,Boardcode,ShipToName,ShipToAddress1,ShipToAddress2,ShipToCity,ShipToState," +
                                        "ShipToZip,ShipToCode,CustomerPO,CustomerPOLine,ClientItem,LoadTag,FirstMachineCode,PiecesPerPallet,FanFold,FanFoldSheetLength,FanFoldUnitHeight," +
                                        "FanFoldPerforationLength,CSCOrderID,DoNotUpgrade,TagPerUnit,AdhesiveCode,EOL";
                                    csv.AppendLine(newLine);
                                    newLine = "Type,Scores,FILLER,EOL";
                                    csv.AppendLine(newLine);
                                    newLine = "Type,SpecialInstructions1,SpecialInstructions2,CustomerOrderNumber,ThreeLRefNo,EOL";
                                    csv.AppendLine(newLine);
                                    //File.AppendAllText(fullOutput, csv.ToString());
                                    //csv.Clear();
                                    newLine = "";

                                    //for existing records true or false
                                    Boolean strExists = false;

                                    //Set parser to Fixed Length Width
                                    parser.TextFieldType = FieldType.FixedWidth;
                                    string[] currentRowFields;
                                    while (!parser.EndOfData)
                                    {
                                        try
                                        {
                                            //Parsing row with a A Record 
                                            // This is to set the lenght of each field in the Fixed Length File
                                            parser.SetFieldWidths(1, 25, 10, 10, 6, 6, 6, 6, 3, 3, 60, 30, 30, 30, 20, 2, 10, 3, 25, 3, 25, 2, 20, 4, 1, 10, 10, 10, 10, 1, 1, 10);
                                            //Read all the fields in the row
                                            currentRowFields = parser.ReadFields();
                                            if (!(currentRowFields == null))
                                            {
                                                if (currentRowFields[0].Contains('A'))
                                                {
                                                    foreach (var field in currentRowFields)
                                                    {
                                                        newLine = newLine + string.Format("{0}", field.Replace(",", "") + ",");
                                                    }
                                                    csv.AppendLine(newLine);
                                                    newLine = "";
                                                }
                                            }

                                            //https://stackoverflow.com/questions/7583770/unable-to-update-the-entityset-because-it-has-a-definingquery-and-no-updatefu
                                            if (!(currentRowFields == null))
                                            {
                                                if (currentRowFields[0].Contains('A'))
                                                {
                                                    try
                                                    {
                                                        //Check if exists
                                                        using (var er = new EDIEntities())
                                                        {
                                                            var f = currentRowFields[2];
                                                            var w = currentRowFields[5];
                                                            var g = currentRowFields[1];
                                                            var err = er.orderfiles.Where(s => (s.Flute == f) &&
                                                                    (s.Width == w) &&
                                                                    (s.Grade == g));

                                                            if (!err.Any())
                                                            {
                                                                strExists = true;
                                                                using (var context = new EDIEntities())
                                                                {
                                                                    var ord = new orderfile()
                                                                    {
                                                                        Type = currentRowFields[0],
                                                                        Grade = currentRowFields[1],
                                                                        Flute = currentRowFields[2],
                                                                        Test = currentRowFields[3],
                                                                        DueDate = currentRowFields[4],
                                                                        Width = currentRowFields[5],
                                                                        Length = currentRowFields[6],
                                                                        Quantity = currentRowFields[7],
                                                                        MaxOver = currentRowFields[8],
                                                                        MaxUnder = currentRowFields[9],
                                                                        Boardcode = currentRowFields[10],
                                                                        ShipToName = currentRowFields[11],
                                                                        ShipToAddress1 = currentRowFields[12],
                                                                        ShipToAddress2 = currentRowFields[13],
                                                                        ShipToCity = currentRowFields[14],
                                                                        ShipToState = currentRowFields[15],
                                                                        ShipToZip = currentRowFields[16],
                                                                        ShipToCode = currentRowFields[17],
                                                                        CustomerPO = currentRowFields[18],
                                                                        CustomerPOLine = currentRowFields[19],
                                                                        ClientItem = currentRowFields[20] == "" ? currentRowFields[2] + "|" + currentRowFields[1] + "|" + currentRowFields[5] : currentRowFields[2] + "|" + currentRowFields[1] + "|" + currentRowFields[5] + "~" + currentRowFields[20],
                                                                        LoadTag = currentRowFields[21],
                                                                        FirstMachineCode = currentRowFields[22],
                                                                        PiecesPerPallet = currentRowFields[23],
                                                                        FanFold = currentRowFields[24],
                                                                        FanFoldSheetLength = currentRowFields[25],
                                                                        FanFoldUnitHeight = currentRowFields[26],
                                                                        FanFoldPerforationLength = currentRowFields[27],
                                                                        CSCOrderID = currentRowFields[28],
                                                                        DoNotUpgrade = currentRowFields[29],
                                                                        TagPerUnit = currentRowFields[30],
                                                                        AdhesiveCode = currentRowFields[31],
                                                                        EOL = String.Format("{0}", filename),
                                                                    };
                                                                    context.orderfiles.Add(ord);
                                                                    context.SaveChanges();
                                                                    idInserted = ord.ID;
                                                                }

                                                                //Parsing row with a X Record
                                                                parser.SetFieldWidths(1, 136, 18);
                                                                currentRowFields = parser.ReadFields();
                                                                if (!(currentRowFields == null))
                                                                {
                                                                    if (currentRowFields[0].Contains('X'))
                                                                    {
                                                                        foreach (var field in currentRowFields)
                                                                        {
                                                                            newLine = newLine + string.Format("{0}", field.Replace(",", "") + ",");
                                                                        }
                                                                        csv.AppendLine(newLine);
                                                                        newLine = "";
                                                                    }
                                                                }

                                                                if (!(currentRowFields == null))
                                                                {
                                                                    if (currentRowFields[0].Contains('X'))
                                                                    {
                                                                        try
                                                                        {
                                                                            using (var context = new EDIEntities())
                                                                            {
                                                                                var scr = new Score()
                                                                                {
                                                                                    orderfileID = idInserted,
                                                                                    Type = currentRowFields[0],
                                                                                    Scores = currentRowFields[1],
                                                                                    FILLER = currentRowFields[2],
                                                                                    EOL = String.Format("{0}", filename),
                                                                                };
                                                                                context.Scores.Add(scr);
                                                                                context.SaveChanges();
                                                                            }
                                                                        }
                                                                        catch (Exception ex)
                                                                        {
                                                                            _log.Info("File " + filename);
                                                                            _log.Info(ex.Message);
                                                                        }
                                                                    }
                                                                }

                                                                //Parsing row with a Z Record
                                                                parser.SetFieldWidths(1, 510, 510, 50, 50);
                                                                currentRowFields = parser.ReadFields();
                                                                if (!(currentRowFields == null))
                                                                {
                                                                    if (currentRowFields[0].Contains('Z'))
                                                                    {
                                                                        foreach (var field in currentRowFields)
                                                                        {
                                                                            newLine = newLine + string.Format("{0}", field.Replace(",", "") + ",");
                                                                        }
                                                                        csv.AppendLine(newLine);
                                                                        newLine = "";
                                                                    }
                                                                }

                                                                if (!(currentRowFields == null))
                                                                {
                                                                    if (currentRowFields[0].Contains('Z'))
                                                                    {
                                                                        try
                                                                        {
                                                                            using (var context = new EDIEntities())
                                                                            {
                                                                                var sp = new SpecialInstruction()
                                                                                {
                                                                                    orderfileID = idInserted,
                                                                                    Type = currentRowFields[0],
                                                                                    SpecialInstruction1 = currentRowFields[1],
                                                                                    SpecialInstruction2 = currentRowFields[2],
                                                                                    CustomerOrderNumber = currentRowFields[3],
                                                                                    ThreePLRefNo = currentRowFields[4],
                                                                                    EOL = String.Format("{0}", filename),
                                                                                };
                                                                                context.SpecialInstructions.Add(sp);
                                                                                context.SaveChanges();
                                                                            }
                                                                        }
                                                                        catch (Exception ex)
                                                                        {
                                                                            _log.Info("File " + filename);
                                                                            _log.Info(ex.Message);
                                                                        }
                                                                    }
                                                                }





                                                            }
                                                            else
                                                            {
                                                                continue;
                                                            }
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        _log.Info("File " + filename);
                                                        _log.Info(ex.Message);
                                                    }
                                                }
                                            }


                                        }
                                        catch (Exception ex)
                                        {
                                            _log.Info("File " + filename);
                                            _log.Info(ex.Message);
                                        }
                                        finally
                                        {

                                        }

                                    }
                                    parser.Close();
                                    _log.Info("Closed the parser");
                                    reader.Close();
                                    _log.Info("Closed the reader");
                                    // if Exists stop here
                                    if (strExists == false)
                                    {
                                        File.AppendAllText(fullReportOutput, csv.ToString());
                                        _log.Info("Created the file");
                                        File.Move(fullReportOutput, fullReportOutput + "_" + DateTime.Now.ToString("yyyy_MM_dd_HHmm_ss_fff") + ".csv");
                                        _log.Info("Moved the file");
                                        File.Delete(PickUpDirPath + "\\" + filename);
                                        _log.Info("Deleting original file");
                                        csv.Clear();
                                        _log.Info("Closed the stringbuilder");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Info(ex.Message);
                            _log.Info("File " + filename + " has an issue");
                        }

                    }

                }
                else
                {
                    _log.Info("No files to process");
                    return;
                }
            }
            else
            {
                _log.Info("PickUp directory does not exist");
            }

            //Send email of file
            //clsFunctions.NetEmail(Properties.Settings.Default.Emails, "CSC File",
            //          EmailFileName, Properties.Settings.Default.SMTPFROMEMAIL,
            //          Properties.Settings.Default.SMTPSERVER,
            //          Properties.Settings.Default.SMTPPort,
            //          Properties.Settings.Default.SMTPUser,
            //          Properties.Settings.Default.SMTPPassword);
            base.Dispose();
            Dispose(true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        #endregion 

        #region ChangeFiles
        public class ResultText
        {
            public string strText { get; set; }    
        }
        public void ChangeFiles()
        {
            FileInfo[] PickUpFiles = new System.IO.DirectoryInfo(Properties.Settings.Default.WorkFolder).GetFiles(Properties.Settings.Default.FileToLookAt).OrderBy(p => p.CreationTime).ToArray();

            if (PickUpFiles.Length > 0)
            {
                //Create a container to rebuild the file with the new field
                var sbText = new StringBuilder();

                foreach (FileInfo filename in PickUpFiles)
                {
                    string fullPath = Properties.Settings.Default.WorkFolder + "\\" + filename;
                    _log.Info("Picked Up filename: " + fullPath);

                    var OutDirPath = Properties.Settings.Default.OutPutFolder;
                    _log.Info("Setting the " + OutDirPath + " out bound folder");

                    var Out_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.OutPutFolder);
                    _log.Info("Getting directory infomation from: " + Out_Dir);

                    _log.Info("Calling the streamreader function");
                    //var reader = File.OpenText(fullPath);
                    using (var reader = new StreamReader(fullPath))
                    {
                        _log.Info("Calling the textfieldparser function");
                        // new Parser and Stream for every line
                        using (var parser = new TextFieldParser(reader))
                        {
                            var newLine = "";
                            //Set parser to Fixed Length Width
                            parser.TextFieldType = FieldType.FixedWidth;
                            string[] currentRowFields;
                            while (!parser.EndOfData)
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

                                            //Check if exists
                                            using (var er = new EDIEntities())
                                            {
                                                var f = currentRowFields[2];
                                                var w = currentRowFields[5];
                                                var g = currentRowFields[1];
                                                var err = er.orderfiles.Where(s => s.Flute == f && s.Width == w && s.Grade == g)
                                                                        .Select(p => new { p.ClientItem })
                                                                        .ToList();
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

                                }
                                catch (Exception ex)
                                {
                                    _log.Info(ex.Message);
                                    _log.Info("File " + filename + " has an issue");
                                }
                                finally
                                {

                                }

                            }
                            if (File.Exists(Out_Dir + "\\" + filename))
                            {
                                File.Delete(Out_Dir + "\\" + filename);
                            }
                            parser.Close();
                            _log.Info("Closed the parser");
                            reader.Close();
                            _log.Info("Closed the reader");
                            File.AppendAllText(Out_Dir + "\\" + filename, sbText.ToString());
                            _log.Info("Created the file");
                            sbText.Clear();
                            _log.Info("Closed the stringbuilder");
                        }

                    }
                    File.Delete(fullPath);
                    _log.Info("Deleting working file");

                }

            }
            base.Dispose();
            Dispose(true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        #endregion
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.Administration;
using System.Xml;
using System.Data.SqlClient;
using System.IO;

namespace DBRestorer
{
    class Program
    {
        private static string _SQLServerDataSource = "";
        private static string _SQLServerUsername = "";
        private static string _SQLServerPassword = "";
        private static string _BackupDBDirectory = "";
        private static string _RestoreDBDirectory = "";
        private static List<string> _IISSites = new List<string>();
        private static List<string> _SourceDirectories = new List<string>();
        private static List<string> _RestoreDirectories = new List<string>();
        private static List<string> _SQLDatabaseNames = new List<string>();
        private static List<string> _SQLDatabaseUsers = new List<string>();
        private static List<string> _MessageList = new List<string>();

        static void Main(string[] args)
        {
            //get the configuration files
            GetSettingValues();

            //stop the iss sites
            var server = new ServerManager();
            foreach(var siteName in _IISSites)
            {
                foreach (var site in server.Sites)
                {
                    if (site != null && siteName.ToLower() == site.Name.ToLower())
                    {
                        WriteLine("The site " + site.Name + " has stopped.");
                        site.Stop();
                    }
                }
            }
            

            //restore the database and folder
            for (var i = 0; i < _SQLDatabaseNames.Count; i++)
            {
                string script = GetScript(_SQLDatabaseNames[i], _SQLDatabaseUsers[i], _BackupDBDirectory, _RestoreDBDirectory);
           
                RunQuery(_SQLDatabaseNames[i], script);

                WriteLine("Restoring database " + _SQLDatabaseNames[i] + ".");

                deleteFolder(_SourceDirectories[i]);
                WriteLine("Delete the source folder: " + _SourceDirectories[i] + ".");

                copyFolder(_RestoreDirectories[i], _SourceDirectories[i]);
                WriteLine("Restore the backup folder: " + _RestoreDirectories[i] + " to " + _SourceDirectories[i] + ".");

            }

            //start the iss sites
            foreach (var siteName in _IISSites)
            {
                foreach (var site in server.Sites)
                {
                    if (site != null && siteName.ToLower() == site.Name.ToLower())
                    {
                        if (site.State == ObjectState.Stopped)
                        {
                            Console.WriteLine("The site " + site.Name + " has started.");
                            site.Start();
                        }
                    }
                }
            }
        }

        static void RunQuery(string databaseName, string sql)
        {
            using (SqlConnection objConnection = new SqlConnection(@"Data Source=" + _SQLServerDataSource + ";Initial Catalog=" + databaseName + ";User ID=" + _SQLServerUsername + ";Password=" + _SQLServerPassword))
            {
                if (objConnection.State == System.Data.ConnectionState.Closed)
                {
                    objConnection.Open();
                }
                SqlCommand objCommand = new SqlCommand(sql, objConnection);
                objCommand.CommandTimeout = 100000;
                objCommand.ExecuteNonQuery();
                if (objConnection.State == System.Data.ConnectionState.Open)
                {
                    objConnection.Close();
                }
            }
        }

        static void GetSettingValues()
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                string xmlText = System.IO.File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/setting.config");
                doc.LoadXml(xmlText.Substring(xmlText.IndexOf(Environment.NewLine)));
                XmlNode nodeParent = doc.SelectSingleNode("/configuration");
                if (nodeParent != null)
                {
                    XmlNodeList childNodes = nodeParent.ChildNodes;
                    foreach (XmlNode node in childNodes)
                    {
                        switch (node.Name)
                        {
                            case "BackupDBDirectory":
                                _BackupDBDirectory = node.InnerText;
                                break;
                            case "RestoreDBDirectory":
                                _RestoreDBDirectory = node.InnerText;
                                break;
                            case "SQLServerDataSource":
                                _SQLServerDataSource = node.InnerText;
                                break;
                            case "SQLServerUsername":
                                _SQLServerUsername = node.InnerText;
                                break;
                            case "SQLServerPassword":
                                _SQLServerPassword = node.InnerText;
                                break;
                            case "SourceDirectories":
                                XmlNodeList sourceDirectoryNodes = node.ChildNodes;
                                foreach (XmlNode dbNode in sourceDirectoryNodes)
                                {
                                    _SourceDirectories.Add(dbNode.InnerText);
                                }
                                break;
                            case "RestoreDirectories":
                                XmlNodeList restoreDirectoryNodes = node.ChildNodes;
                                foreach (XmlNode dbNode in restoreDirectoryNodes)
                                {
                                    _RestoreDirectories.Add(dbNode.InnerText);
                                }
                                break;
                            case "SQLDatabaseNames":
                                XmlNodeList dbNodes = node.ChildNodes;
                                foreach (XmlNode dbNode in dbNodes)
                                {
                                    _SQLDatabaseNames.Add(dbNode.InnerText);
                                }
                                break;
                            case "SQLDatabaseUsers":
                                XmlNodeList dbUserNodes = node.ChildNodes;
                                foreach (XmlNode dbNode in dbUserNodes)
                                {
                                    _SQLDatabaseUsers.Add(dbNode.InnerText);
                                }
                                break;
                            case "IISSites":
                                XmlNodeList iisSiteNodes = node.ChildNodes;
                                foreach (XmlNode dbNode in iisSiteNodes)
                                {
                                    _IISSites.Add(dbNode.InnerText);
                                }
                                break;
                        }
                    }
                }

                _MessageList.Add(">>> Configuration settings have been read successfully." + Environment.NewLine);
                WriteLine(">>> Configuration settings have been read successfully.");
            }
            catch (Exception ex)
            {
                _MessageList.Add(">>> Error reading configuration: " + ex.Message.ToString() + Environment.NewLine);
                WriteLine(">>> Error reading configuration: " + ex.Message.ToString());
            }
        }

        static void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        static void ReadLine(string message)
        {
            WriteLine(message);
            Console.ReadLine();
        }

        static void deleteFolder(string FolderName)
        {
            DirectoryInfo dir = new DirectoryInfo(FolderName);

            foreach (FileInfo fi in dir.GetFiles())
            {
                fi.Delete();
            }

            foreach (DirectoryInfo di in dir.GetDirectories())
            {
                deleteFolder(di.FullName);
                di.Delete();
            }
        }

        static void copyFolder(string strSource, string strDestination)
        {
            if (!Directory.Exists(strDestination))
            {
                Directory.CreateDirectory(strDestination);
            }

            DirectoryInfo dirInfo = new DirectoryInfo(strSource);
            FileInfo[] files = dirInfo.GetFiles();
            foreach (FileInfo tempfile in files)
            {
                tempfile.CopyTo(Path.Combine(strDestination, tempfile.Name));
            }

            DirectoryInfo[] directories = dirInfo.GetDirectories();
            foreach (DirectoryInfo tempdir in directories)
            {
                copyFolder(Path.Combine(strSource, tempdir.Name), Path.Combine(strDestination, tempdir.Name));
            }
        }

        #region sql scripts
        static string GetScript(string DatabaseName, string DatabaseUser, string BackupDBDirectory, string RestoreDBDirectory)
    {
            string script = @"  USE [master]
                                
                            DECLARE @dbname sysname
                            SET @dbname = '{DATABASENAME}'

                            DECLARE @spid int
                            SELECT @spid = min(spid) from master.dbo.sysprocesses where dbid = db_id(@dbname)
                            WHILE @spid IS NOT NULL
                            BEGIN
                            EXECUTE ('KILL ' + @spid)
                            SELECT @spid = min(spid) from master.dbo.sysprocesses where dbid = db_id(@dbname) AND spid > @spid
                            END

                            RESTORE DATABASE[{DATABASENAME}]
                            FROM DISK = N'{BACKUPDIRECTORY}{DATABASENAME}.bak' WITH FILE = 1,
                            MOVE N'{DATABASENAME}' TO N'{RESTOREDBDIRECTORY}{DATABASENAME}.mdf',
                            MOVE N'{DATABASENAME}_log' TO N'{RESTOREDBDIRECTORY}{DATABASENAME}.ldf',
                            NOUNLOAD,  REPLACE,  STATS = 10
                                
                            USE [{DATABASENAME}]
                                
                            IF  EXISTS(SELECT * FROM sys.database_principals WHERE name = N'{DATABASEUSER}')
                            DROP USER[{DATABASEUSER}]
                               
                            USE [{DATABASENAME}]
                               
                            CREATE USER[{DATABASEUSER}] FOR LOGIN[{DATABASEUSER}]
                               
                            USE [{DATABASENAME}]
                               
                            ALTER USER[{DATABASEUSER}] WITH DEFAULT_SCHEMA =[dbo]
                                
                            USE [{DATABASENAME}]
                             
                            EXEC sp_addrolemember N'db_owner', N'{DATABASEUSER}'
                                
                        ";

            script = script.Replace("{DATABASENAME}", DatabaseName);
            script = script.Replace("{DATABASEUSER}", DatabaseUser);
            script = script.Replace("{BACKUPDIRECTORY}", BackupDBDirectory);
            script = script.Replace("{RESTOREDBDIRECTORY}", RestoreDBDirectory);
            return script;
        }
        #endregion
    }
}

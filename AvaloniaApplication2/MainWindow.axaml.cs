using System;
using System.Data;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DevOne.Security.Cryptography.BCrypt;
using MySql.Data.MySqlClient;

namespace AvaloniaApplication2;

public partial class MainWindow : Window
{
    private string ConnectionString;
    private string uID;
    private string timestamp;
    private string logtype;
    DispatcherTimer timer = new DispatcherTimer();
public MainWindow()
        {
            InitializeComponent();
            timer = new DispatcherTimer();
            
            string connectionString = "server=195.20.234.224;port=5500;user=TimyBase;password=Test1234;database=TimyBase;Allow User Variables=True;";
            ConnectionString = connectionString;
            
            if(File.Exists(@"../config.txt"))
            {
                string filecon = File.ReadAllText(@"../config.txt");
                if (filecon != "")
                {
                    //serch for username: and password: in the file
                    int startUsername = filecon.IndexOf("username:") + 9;
                    int endUsername = filecon.IndexOf(";", startUsername);
                    string username = filecon.Substring(startUsername, endUsername - startUsername);

                    int startPassword = filecon.IndexOf("password:") + 9;
                    int endPassword = filecon.IndexOf(";", startPassword);
                    string password = filecon.Substring(startPassword, endPassword - startPassword);
                    
                    //call the login handler
                    userLogin(username, password, connectionString);
                    
                    char seper = '|';
                    string[] split = filecon.Split(seper);
                    
                    string uID = userLogin(username, password, connectionString);
                    
                    string timestamp = GetUserTimeToWork(uID, connectionString);
                    
                    
                    
                }
            }else
            {
                //create the file in run directory
                File.Create("../config.txt");
                
                logingrid.IsVisible = true;
                //write text in config file from Usesrname and Password
                File.WriteAllText(@"./config.txt", "username: " + Username.Text + "; \npassword: " + Password.Text + ";");

                uID = userLogin(Username.Text, Password.Text, ConnectionString);

            }
        }

        private string GetUserTimeToWork(string uID, string connectionString)
        {
            string timestamp = string.Empty;

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("getuserstimetowork", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@user_ID", this.uID);
                    var timeParam = new MySqlParameter("@timeover", MySqlDbType.VarChar)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(timeParam);

                    cmd.ExecuteNonQuery();

                    string time = timeParam.Value.ToString();
                    Console.WriteLine("Time: " + time);
                }
            }

            return timestamp;
        }
        
        private string userLogin(string username, string password, string connectionString)
        {
            string uID = string.Empty;
            bool userFound = false;

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // Retrieve all hashed passwords for the given username
                using (var cmd = new MySqlCommand("SELECT u.id, u.password FROM users as u WHERE name = '"+ username +"'", conn))
                {
                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        string hashedPassword = reader.GetString(1);

                        // Replace $2y$ with $2a$ in the hashed password
                        if (hashedPassword.StartsWith("$2y$"))
                        {
                            hashedPassword = "$2a$" + hashedPassword.Substring(4);
                        }

                        // Check if the entered password matches the stored hashed password
                        if (BCryptHelper.CheckPassword(password, hashedPassword))
                        {
                            UInt64 uID1 = reader.GetUInt64(0);
                            this.uID = uID1.ToString();
                            userFound = true;
                            break;
                        }
                    }
                }
            }

            if (!userFound)
            {
                Error_Message.IsVisible = true;
            }

            // Set the timer interval to 5 minutes
            timer.Interval = TimeSpan.FromMinutes(5);

            // Subscribe to the Tick event
            timer.Tick += Timer_Tick;

            // Start the timer
            timer.Start();

            return uID;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            string startTimeString = timestamp;
            
            DateTime startTime = DateTime.Parse(startTimeString);
            
            DateTime currentTime = DateTime.Now;
            
            TimeSpan durationWorked = currentTime - startTime;
            
            txt_time.Text = durationWorked.ToString();
        }
        

        private void passwordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                userLogin(Username.Text, HashPassword(Password.Text), ConnectionString);
            }
        }
        

        private void Password_OnKeyDown(object? sender, KeyEventArgs e)
        {
            //check if the user pressed the enter key
            if (e.Key == Key.Enter)
            {
                //call the login handler
                userLogin(Username.Text, HashPassword(Password.Text), ConnectionString);
            }
        }

        private void ComeOrGo(string uID)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    conn.Open();

                    // Create a MySqlCommand object
                    MySqlCommand cmd =
                        new MySqlCommand(
                            "CALL InsertGehenIfKommenExists( null, '" + uID +
                            "' ,@uName, @type); SELECT @uName, @type;", conn);

                    // Execute the command
                    MySqlDataReader reader = cmd.ExecuteReader();

                    // Read the results
                    while (reader.Read())
                    {
                        string uName = reader.GetString(0);
                        logtype = reader.GetString(1);

                        // Print the results
                        Console.WriteLine("uName: " + uName);
                        Console.WriteLine("type: " + logtype);

                        if (logtype == "Kommen")
                        {
                            btn_come.IsEnabled = false;
                            btn_go.IsEnabled = true;
                        }
                        else
                        {
                            btn_go.IsEnabled = false;
                            btn_come.IsEnabled = true;
                        }
                    }
                }
                catch (MySqlException ex)
                {
                    // Handle exception if there are any problems opening the connection
                    Console.WriteLine(ex.Message);
                }
            }

        }

        static string HashPassword(string input)
        {
            string salt = BCryptHelper.GenerateSalt(12); // Generate a salt with a cost factor of 12
            string hashedPassword = BCryptHelper.HashPassword(input, salt);
            return hashedPassword;
        }
}
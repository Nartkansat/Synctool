using System;
using System.Data;
using MySqlConnector;

var connString = "Server=192.168.1.198;Port=3306;Database=synctool;Uid=nart;Pwd=Nart.123.Nart;Command Timeout=15;Allow User Variables=true;";
using var conn = new MySqlConnection(connString);
conn.Open();

var cmd1 = new MySqlCommand("ALTER TABLE ManualCampaigns ADD COLUMN DiscountPrice decimal(18,2) NULL;", conn);
try { cmd1.ExecuteNonQuery(); Console.WriteLine("DiscountPrice added."); } catch(Exception ex) { Console.WriteLine(ex.Message); }

var cmd2 = new MySqlCommand("ALTER TABLE ManualCampaignProducts ADD COLUMN IsTargetProduct tinyint(1) NOT NULL DEFAULT 0;", conn);
try { cmd2.ExecuteNonQuery(); Console.WriteLine("IsTargetProduct added."); } catch(Exception ex) { Console.WriteLine(ex.Message); }

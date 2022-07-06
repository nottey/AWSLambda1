using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.RDS;
using Amazon.S3;
using Amazon.S3.Model;
using AWSLambda1.Models;
using MySqlConnector;
using Newtonsoft.Json;
using System.Data;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSLambda1;

public class Function
{
    private IAmazonS3 S3Client { get; set; }
    private IAmazonRDS rdsClient { get; set; }
    private MySqlConnection Connection;
    private string endpoint = System.Environment.GetEnvironmentVariable("RDSEndpoint");
    private string db = System.Environment.GetEnvironmentVariable("Database");
    private string pwd = "Nottage2!!";
    private string user = "vs-test";
    private DataTable snkrTable = new DataTable();

    private DataTable dataTable = new DataTable();
    private MySqlDataAdapter dataAdapter = null;

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        S3Client = new AmazonS3Client();
        rdsClient = new AmazonRDSClient();
    }

    /// <summary>
    /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
    /// </summary>
    /// <param name="s3Client"></param>
    public Function(IAmazonS3 s3Client, IAmazonRDS rdsClient)
    {
        this.S3Client = s3Client;
        this.rdsClient = rdsClient;
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="evnt"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<string?> FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        var s3Event = evnt.Records?[0].S3;
        if (s3Event == null)
        {
            return null;
        }
        if (evnt.Records?[0].EventName.Value != "ObjectCreated:Put")
            return null;
        string vc_msg = "";
        try
        {
            this.Connection = new MySqlConnection($"server={endpoint};user={user};database={db};port=3306;password={pwd};Convert Zero Datetime=True");
            Console.WriteLine($"server={endpoint};user={user};database={db};port=3306;password={pwd};Convert Zero Datetime=True");
            //Json is dropped in S3.  After loaded read that file

            #region Get loaded Object
            vc_msg += "Inside Function. " + s3Event.Object.Key + ", " + s3Event.Bucket.Name + ", "
                + s3Event.Bucket.Arn;
            //Take the loaded file and import data into RDS instance 
            GetObjectResponse responseObj = new GetObjectResponse();
           responseObj = await this.S3Client.GetObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key);
           StreamReader reader = new StreamReader(responseObj.ResponseStream);
           string content = reader.ReadToEnd();
           var fileIn = JsonConvert.DeserializeObject<dynamic>(content); 
          SneakerModel snkIn = JsonConvert.DeserializeObject<SneakerModel>(content);
            #endregion Get loaded Object 

            #region Insert Into RDS
            if (snkIn != null)
            { 
                Console.WriteLine("PRE UPC"); 
                bool exists = await upcExists(snkIn.UPC);
                vc_msg = "|| Post UPC Check: " + exists.ToString();
                Console.WriteLine(vc_msg);

                SneakerEntity sneaker = new SneakerEntity(snkIn);
                if (exists == false)
                {
                    vc_msg = "UPC: " + snkIn.UPC + " doesn't exist.";
                    insertSneaker(sneaker);
                }
                else
                {
                    vc_msg = "UPC: " + snkIn.UPC + " exists.";
                    updateSneaker(sneaker);
                }
            }
            #endregion Insert Into RDS 
        
            //Method to fill snkrTable with records from RDS
            //get_sneakerTablerds(); 
            return "Msg: " + vc_msg;
        }
        catch (Exception e)
        {
            vc_msg += "Error: " + e.Message;
            context.Logger.LogInformation(vc_msg);
            context.Logger.LogInformation(e.Message);
            context.Logger.LogInformation(e.StackTrace);
            throw;
        }
    }

    private async Task<DataTable> get_sneakerTablerds()
    {
        //DataTable snkrTable = new DataTable();
        snkrTable.TableName = "sneakers";
        MySqlDataAdapter snkrAdapter = get_MySqlTableContext(snkrTable, this.Connection);
        List<SneakerEntity> entities = new List<SneakerEntity>();

        foreach (DataRow row in snkrTable.Rows)
        {
            Console.WriteLine(row.ToString());
            entities.Add(new SneakerEntity(row));
        }

        snkrTable.Rows.Add(entities[0]);
        //Cant update using adapter here
        //this.dataAdapter.Update(snkrTable);
        //bool exists = await upcExists(entities[0].UPC);
        //if (exists == false)
        //    insertSneaker();
        snkrAdapter.Update(snkrTable);
        return snkrTable;
    }

    private async Task<Boolean> upcExists(string s_upc)
    {
        var connection = this.Connection;
        string findUPC_cmd = "Select UPC from sneakers s where s.UPC = '" + s_upc + "'";
        MySqlCommand command = new MySqlCommand(findUPC_cmd, connection);
        bool recordExists = false;
        try
        {
            Console.WriteLine("Pre connection open");
            //On VPC security for RDS must open connection to Lambda function.  Setting open to all is bad for prod
            await connection.OpenAsync();
            MySqlDataReader reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                Console.WriteLine("UPC: " + reader.GetString(0) + " exists in Sneaker table!");
                if (reader.IsDBNull(0)) recordExists = false;
                else recordExists = true;
            }
            reader.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        await connection.CloseAsync();
        Console.WriteLine("Connection closed");
        return recordExists;
    }

    private void updateSneaker(SneakerEntity in_sneaker)
    {
        var connection = this.Connection;
        connection.Open();
        List<SneakerEntity> entities = new List<SneakerEntity>();
        entities.Add(in_sneaker);
        Console.WriteLine("Begin Update.");
        var command = new MySqlCommand("updateSneaker_vs", connection);
        command.CommandType = System.Data.CommandType.StoredProcedure;
        foreach (SneakerEntity sneaker in entities)
        {
            command.Parameters.Add(new MySqlParameter("in_Brand", SqlDbType.VarChar) { Value = sneaker.Brand });
            command.Parameters.Add(new MySqlParameter("in_Model", SqlDbType.VarChar) { Value = sneaker.Model });
            command.Parameters.Add(new MySqlParameter("in_Name", SqlDbType.VarChar) { Value = sneaker.Name });
            command.Parameters.Add(new MySqlParameter("in_Type", SqlDbType.VarChar) { Value = sneaker.Type });
            command.Parameters.Add(new MySqlParameter("in_ReleaseDate", SqlDbType.VarChar) { Value = sneaker.ReleaseDate });
            command.Parameters.Add(new MySqlParameter("in_purchaseDate", SqlDbType.VarChar) { Value = sneaker.PurchDate });
            command.Parameters.Add(new MySqlParameter("in_id", SqlDbType.VarChar) { Value = sneaker.ID });
            command.Parameters.Add(new MySqlParameter("in_upc", SqlDbType.VarChar) { Value = sneaker.UPC });
            command.Parameters.Add(new MySqlParameter("in_colorway", SqlDbType.VarChar) { Value = sneaker.Colorway1 });
            command.Parameters.Add(new MySqlParameter("in_retailPrice", SqlDbType.Float) { Value = sneaker.RetailPrice });
            command.Parameters.Add(new MySqlParameter("in_imgSrc", SqlDbType.VarChar) { Value = sneaker.ImgSrc });
            command.Parameters.Add(new MySqlParameter("in_raffle", SqlDbType.VarChar) { Value = sneaker.Raffle });
            command.Parameters.Add(new MySqlParameter("in_featured", SqlDbType.VarChar) { Value = sneaker.Featured });
            command.Parameters.Add(new MySqlParameter("in_link", SqlDbType.VarChar) { Value = sneaker.Link1 });
            command.Parameters.Add(new MySqlParameter("in_inCollection", SqlDbType.VarChar) { Value = sneaker.InCollection });
            /*
            var rank = new MySqlParameter("p_rank", SqlDbType.Int) { Direction = ParameterDirection.Output };
            command.Parameters.Add(rank);
            */
            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("End Update.");
        }
    }

    private void insertSneaker(SneakerEntity in_sneaker)
    {
        var connection = this.Connection;
        connection.Open();
        List<SneakerEntity> entities = new List<SneakerEntity>();
        entities.Add(in_sneaker);

        var command = new MySqlCommand("insertIntoSneaker_vs", connection);
        command.CommandType = System.Data.CommandType.StoredProcedure;
        foreach (SneakerEntity sneaker in entities)
        {
            command.Parameters.Add(new MySqlParameter("in_Brand", SqlDbType.VarChar) { Value = sneaker.Brand });
            command.Parameters.Add(new MySqlParameter("in_Model", SqlDbType.VarChar) { Value = sneaker.Model });
            command.Parameters.Add(new MySqlParameter("in_Name", SqlDbType.VarChar) { Value = sneaker.Name });
            command.Parameters.Add(new MySqlParameter("in_Type", SqlDbType.VarChar) { Value = sneaker.Type });
            command.Parameters.Add(new MySqlParameter("in_ReleaseDate", SqlDbType.VarChar) { Value = sneaker.ReleaseDate });
            command.Parameters.Add(new MySqlParameter("in_purchaseDate", SqlDbType.VarChar) { Value = sneaker.PurchDate });
            command.Parameters.Add(new MySqlParameter("in_id", SqlDbType.VarChar) { Value = sneaker.ID });
            command.Parameters.Add(new MySqlParameter("in_upc", SqlDbType.VarChar) { Value = sneaker.UPC });
            command.Parameters.Add(new MySqlParameter("in_colorway", SqlDbType.VarChar) { Value = sneaker.Colorway1 });
            command.Parameters.Add(new MySqlParameter("in_retailPrice", SqlDbType.Float) { Value = sneaker.RetailPrice });
            command.Parameters.Add(new MySqlParameter("in_imgSrc", SqlDbType.VarChar) { Value = sneaker.ImgSrc });
            command.Parameters.Add(new MySqlParameter("in_raffle", SqlDbType.VarChar) { Value = sneaker.Raffle });
            command.Parameters.Add(new MySqlParameter("in_featured", SqlDbType.VarChar) { Value = sneaker.Featured });
            command.Parameters.Add(new MySqlParameter("in_link", SqlDbType.VarChar) { Value = sneaker.Link1 });
            command.Parameters.Add(new MySqlParameter("in_inCollection", SqlDbType.VarChar) { Value = sneaker.InCollection });
            command.Parameters.Add(new MySqlParameter("in_LastModified", SqlDbType.DateTime) { Value = DateTime.Now });
            /*
            var rank = new MySqlParameter("p_rank", SqlDbType.Int) { Direction = ParameterDirection.Output };
            command.Parameters.Add(rank);
            */
            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    public MySqlDataAdapter get_MySqlTableContext(System.Data.DataTable inTable, MySqlConnection connection)
    {
        MySqlDataAdapter dataAdapter = new MySqlDataAdapter(string.Format("SELECT * FROM {0}",
            inTable.TableName), connection);
        dataAdapter.UpdateBatchSize = 50;

        // Using workaround for MySQL Connector bug described at:
        // http://bugs.mysql.com/bug.php?id=39815
        // Dispose the builder before setting adapter commands.
        MySqlCommandBuilder builder = new MySqlCommandBuilder(dataAdapter);
        MySqlCommand updateCommand = builder.GetUpdateCommand();
        MySqlCommand insertCommand = builder.GetInsertCommand();
        MySqlCommand deleteCommand = builder.GetDeleteCommand();

        builder.Dispose();
        dataAdapter.UpdateCommand = updateCommand;
        dataAdapter.InsertCommand = insertCommand;
        dataAdapter.DeleteCommand = deleteCommand;
        dataAdapter.Fill(inTable);
        return dataAdapter;
    }
}
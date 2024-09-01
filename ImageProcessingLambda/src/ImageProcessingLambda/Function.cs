using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ImageProcessingLambda;

public class Function
{
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly string _bucketName = "bucket-name";
    private readonly string _tableName = "table-name";

    public Function()
    {
        _s3Client = new AmazonS3Client();
        _dynamoDbClient = new AmazonDynamoDBClient();
    }

    public async Task<string> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            var body = request.Body;

            var imageRequest = JsonSerializer.Deserialize<ImageRequest>(body);

            // Decode Base64 string to byte array
            var imageBytes = Convert.FromBase64String(imageRequest.Base64Image);

            var imageId = Guid.NewGuid().ToString();
            var imageKey = $"{imageId}.jpeg";

            // Upload to S3
            using (var stream = new MemoryStream(imageBytes))
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = imageKey,
                    InputStream = stream,
                    ContentType = "image/jpeg"
                };

                await _s3Client.PutObjectAsync(putRequest);
            }

            // Save to DynamoDB
            var table = Table.LoadTable(_dynamoDbClient, _tableName);
            var document = new Document(){
                ["ImageId"] = imageId,
                ["S3Url"] = $"https://{_bucketName}.s3.amazonaws.com/{imageKey}",
                ["UploadDate"] = DateTime.UtcNow.ToString("o"),
            };

            await table.PutItemAsync(document);

            var response = document["S3Url"].AsString();
            return response;;
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error: {ex.Message}");
            throw;
        }
    }

    public class ImageRequest
    {
        [JsonPropertyName("base64Image")]
        public string Base64Image { get; set; }
    }
   
}

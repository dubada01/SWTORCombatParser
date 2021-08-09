using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public class S3Connection
    {
        private  IAmazonS3 _s3Client;
        private string _bucketName = "swtor-parse-logs";
        public S3Connection()
        {
            _s3Client = new AmazonS3Client(RegionEndpoint.USWest2);
        }
        public PutObjectResponse UploadLog(string jsonLogContents,string raidGroup, string logName)
        {
            return UploadObjectFromContentAsync(_s3Client, _bucketName, raidGroup+"/"+logName, jsonLogContents);
        }
        public List<string> GetLogs(string raidGroup)
        {
            return GetObjectsFromPrefix(raidGroup, _bucketName);
        }
        private List<string> GetObjectsFromPrefix(string prefix, string bucketName)
        {
            var dataToReturn = new List<string>();
            ListObjectsV2Request request = new ListObjectsV2Request() { BucketName = bucketName, Prefix = prefix };
            var logPathsForRaid = _s3Client.ListObjectsV2Async(request).Result;
            foreach(var logFile in logPathsForRaid.S3Objects)
            {
                using (GetObjectResponse response = _s3Client.GetObjectAsync(bucketName, logFile.Key).Result)
                {
                    using (StreamReader reader = new StreamReader(response.ResponseStream))
                    {
                        string contents = reader.ReadToEnd();
                        dataToReturn.Add(contents);
                    }
                }
                
            }
            return dataToReturn;
        }
        private static PutObjectResponse UploadObjectFromContentAsync(IAmazonS3 client,
    string bucketName,
    string objectName,
    string content)
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectName,
                ContentBody = content
            };
            PutObjectResponse response = client.PutObjectAsync(putRequest).Result;
            return response;
        }
    }
}

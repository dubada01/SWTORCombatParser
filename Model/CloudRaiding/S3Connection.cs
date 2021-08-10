using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public class RemoteLog
    {
        public RemoteLog(string logName, string logContents)
        {
            Name = logName;
            Contents = logContents;
        }
        public string Name;
        public string Contents;
    }
    public class S3Connection
    {
        private  IAmazonS3 _s3Client;
        private string _bucketName = "swtor-parse-logs";
        private string _raidGroupsPrefix = "raid-groups/";
        public S3Connection()
        {
            _s3Client = new AmazonS3Client(RegionEndpoint.USWest2);
        }
        public PutObjectResponse UploadLog(string jsonLogContents,string raidGroup, string logName)
        {
            return UploadObjectFromContentAsync(_raidGroupsPrefix+raidGroup+"/"+logName, jsonLogContents);
        }
        public List<RemoteLog> GetLogs(string raidGroup)
        {
            return GetObjectsFromPrefix(raidGroup);
        }
        public bool TryAddRaidTeam(string raidGroup)
        {
            if (CheckForPrefix(raidGroup))
                return false;
            else
            {
                var response = UploadObjectFromContentAsync(_raidGroupsPrefix+raidGroup+"/", "");
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                    return true;
                else
                    return false;
            }
        }
        private bool CheckForPrefix(string prefix)
        {
            ListObjectsV2Request request = new ListObjectsV2Request() { BucketName = _bucketName,Prefix = _raidGroupsPrefix, Delimiter="/"};
            var raidGroups = _s3Client.ListObjectsV2Async(request).Result;
            foreach (var logFile in raidGroups.S3Objects)
            {
                if (logFile.Key.Contains(prefix))
                    return true;
            }
            return false;
        }
        private List<RemoteLog> GetObjectsFromPrefix(string prefix)
        {
            var dataToReturn = new List<RemoteLog>();
            ListObjectsV2Request request = new ListObjectsV2Request() { BucketName = _bucketName, Prefix = _raidGroupsPrefix + prefix };
            var logPathsForRaid = _s3Client.ListObjectsV2Async(request).Result;
            foreach(var logFile in logPathsForRaid.S3Objects)
            {
                using (GetObjectResponse response = _s3Client.GetObjectAsync(_bucketName, logFile.Key).Result)
                {
                    using (StreamReader reader = new StreamReader(response.ResponseStream))
                    {
                        string contents = reader.ReadToEnd();
                        if (string.IsNullOrEmpty(contents))
                            continue;
                        dataToReturn.Add(new RemoteLog(response.Key.Split('/').Last(), contents));
                    }
                }
                
            }
            return dataToReturn;
        }

        private PutObjectResponse UploadObjectFromContentAsync(string objectName,string content)
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectName,
                ContentBody = content
            };
           
            PutObjectResponse response = _s3Client.PutObjectAsync(putRequest).Result;
            return response;
        }
    }
}

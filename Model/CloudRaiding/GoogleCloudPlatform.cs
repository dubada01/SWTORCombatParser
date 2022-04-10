using Google.Cloud.Vision.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class GoogleCloudPlatform
    {
        private static string creds = "{\n  \"type\": \"service_account\",\n  \"project_id\": \"test-vision-345418\",\n  \"private_key_id\": \"6fc2eedd6172d5595b9729be30b1bfe44588a2ea\",\n  \"private_key\": \"-----BEGIN PRIVATE KEY-----\\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQChgrkDIgeY3K+V\\ntpFzRKFiYs58s3UYcYdpmzUiCsltPQ4i3CoEnElPkQeCGRcumhaTjNUWA22X+idc\\nPr+pZxTaSWaUu1GXB2ASEJoFivweSaVUmhjJrOu+p5EaS5UQKMdJ48j/DYLxGcg/\\ny8f8/RNmbqwerB3b4gY1wPVGCAbNvFT7zfgKz6xuCNywgDTYPeFxEedWrhsCRN/h\\nyGQ6e/Gjs9uIemHBjTsCVZbc9QlUZAqzPN5DpypaWAoF4DnBEt5HchlBTGaxXSwd\\ngIBF9DF1rNtfMdhjK7PyOPOX0qq1Fc0NDDLH4rMAIhAxOhrqt0FbN5SYOBxzhcPx\\nF/Q1LdzHAgMBAAECggEAHqLs1F7OoRzvg48jjQFuOXrznyztX8sdPBeQXxo7tih4\\nN32azNAWiezBR1jaEGFzOd7jhq70yXjOoY1Xjts6VePJfRoBMoxYGWUjyjxgcaBX\\np7IpVIwkFcC0YhAHQQ7zKp12QYc/GNvio9NUZrVcyjfhF1pfuZdoxSaKgBPCsqCj\\nCMW0V0i9IygqGOcaXaFjU4KWXs8/2sV95LpvlEm5463qxI5HpeseGZ6USFTeapTw\\n9eoHW+P5xQnl5wMAsZoD5D3bK1MlnE8CBPsKTsxGWWJUE3r8H0RBUp7xdRiGufn/\\nzXNbhH/xDlawNvFkKZZDwunvm32EPxifdI8bjUuQHQKBgQDael2W/B3rJLWFyKU8\\nLdpsTjf6r9csHRvYKxuMLi+kwxIu5CSjEQAr797uGF5+6Si398eT7jmuuaSIqvDE\\nPcn/+kG13Z7a4FB4aTaPsYs9reyohsZegpYU5Mv/fdRPhv1SHf3Zcd37aHUDzmM1\\nz5zg/zZAJ6Vg+wAWPR/CEldj1QKBgQC9P7jnocRPkIeG3XOsDi0dKmPY9O98nV3R\\nBlTyg/o2q4bNQvXutUWHe+KJcrKNWrgdbj7Y+MNkmsmfi9Mjvyy1MWIxoiEn3sBw\\nnOzNe4g4L7sPv7JlT2WooXOqSSGFqb2hDV2Munifc69X6Fx9J1OOOe4lrMYYpq8J\\nx7ogcEO4KwKBgQCzsbduarj04YGHosr83z3qnL8AKkaRGvP+7R3AQ3CeVG+NL8pO\\ncLoyaR1zuYlnWsBJ57s4GdVJt5jza52R2rxdFOmc+sYggiTNlMPylfXPalDfH9Li\\ngweL8c8zubu5GW8bbl2Ozk/k6zprQgJpjPQcPuzRAYrNZETjBa0sQ0erbQKBgBf0\\nwQtRRvBrczx3O/VDtiJDA3CrUMWNhhq3mnk4i6vv+phxKYCWIb8Mx5hulHugSD4x\\nfnMoylMp5Ov1XzzfLmGhZrSxuVC6udGHi9JXGN9D64IK2iJI8q1uAp5Ds6Kf4glJ\\nD9aIpExK8J9IIq0VFVajrqnGPS1RrEaoqb8BdxERAoGBAIdlBcfxdSie06FO2dqp\\nM2YToUPRGVauuj2bnH1arofL59RH17BlhJSTrt/0X7wJ1+IO8Wht3Z9hlsShryMu\\nCZbx1Wk3TYOUONVi9WUNx8VTFJ0iNQ8i0borZerSvFVQQK084oveKepoGSocK1HI\\nzEgOXiQMLVzIGISwXUZkM8/h\\n-----END PRIVATE KEY-----\\n\",\n  \"client_email\": \"test-vision-swtor@test-vision-345418.iam.gserviceaccount.com\",\n  \"client_id\": \"103637147853379723621\",\n  \"auth_uri\": \"https://accounts.google.com/o/oauth2/auth\",\n  \"token_uri\": \"https://oauth2.googleapis.com/token\",\n  \"auth_provider_x509_cert_url\": \"https://www.googleapis.com/oauth2/v1/certs\",\n  \"client_x509_cert_url\": \"https://www.googleapis.com/robot/v1/metadata/x509/test-vision-swtor%40test-vision-345418.iam.gserviceaccount.com\"\n}\n";
        private static ImageAnnotatorClient _currentClient;
        public static ImageAnnotatorClient GetClient()
        {
            if(_currentClient == null)
            {
                ImageAnnotatorClientBuilder clientBuilder = new ImageAnnotatorClientBuilder() { JsonCredentials = creds };
                _currentClient = clientBuilder.Build();
            }
            return _currentClient;
        }
    }
}

using Microsoft.Maui.Networking;
using RestSharp;
using System;
using System.Threading.Tasks;

namespace MauiCamera
{
    internal class Data
    {
        //private string _baseUrl = "https://redsixapiuat.pdsglobal.com";
        private string _baseUrl = "http://192.168.20.50:7230";


        //public async Task<SocialFeedPayload> GetUserFeed()
        //{
        //    var res = await GetAsync<PayloadWrapper<SocialFeedPayload>>(_baseUrl, "RedSix/GetUserFeed2");
        //    return res.Data;
        //}

        public async Task<T> GetAsync<T>(string baseUrl, string endpointPath)
        {
            var request = new RestRequest(endpointPath, Method.Get);


            try
            {
                RestClient restClient = new RestClient(baseUrl);

                if (Connectivity.Current.NetworkAccess != NetworkAccess.None)
                {
                    var response = await restClient.GetAsync<T>(request);
                    return response;
                }
                else
                    throw new Exception("Oops");
            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.Message);
                throw;
            }
        }

    }
}

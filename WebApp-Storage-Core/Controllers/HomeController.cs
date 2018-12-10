//----------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
//----------------------------------------------------------------------------------
// The example companies, organizations, products, domain names,
// e-mail addresses, logos, people, places, and events depicted
// herein are fictitious.  No association with any real company,
// organization, product, domain name, email address, logo, person,
// places, or events is intended or should be inferred.

namespace WebApp_Storage_Core.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.IO;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Http;
    using WebApp_Storage_Core.Models;
    using System.Diagnostics;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Azure Blob Storage Photo Gallery - Demonstrates how to use the Blob Storage service.
    /// Blob storage stores unstructured data such as text, binary data, documents or media files.
    /// Blobs can be accessed from anywhere in the world via HTTP or HTTPS.
    ///
    /// Note: This sample uses the .NET 4.5 asynchronous programming model to demonstrate how to call the Storage Service using the
    /// storage client libraries asynchronous API's. When used in real applications this approach enables you to improve the
    /// responsiveness of your application. Calls to the storage service are prefixed by the await keyword.
    ///
    /// Documentation References:
    /// - What is a Storage Account - http://azure.microsoft.com/en-us/documentation/articles/storage-whatis-account/
    /// - Getting Started with Blobs - http://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/
    /// - Blob Service Concepts - http://msdn.microsoft.com/en-us/library/dd179376.aspx
    /// - Blob Service REST API - http://msdn.microsoft.com/en-us/library/dd135733.aspx
    /// - Blob Service C# API - http://go.microsoft.com/fwlink/?LinkID=398944
    /// - Delegating Access with Shared Access Signatures - http://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-shared-access-signature-part-1/
    /// </summary>

    public class HomeController : Controller
    {
        static CloudBlobClient blobClient;
        const string blobContainerName = "cont1";
        static CloudBlobContainer blobContainer;
        private readonly string connectionString;

        public HomeController(IConfiguration configuration)
        {
            var accountName = configuration["STORAGE_ACCOUNT_NAME"];
            var accountKey = configuration["STORAGE_ACCOUNT_KEY"];
            var storageHost = configuration["STORAGE_HOST"];
            var storagePort = configuration["STORAGE_PORT"];
            //var host = configuration.GetSection("IOTEDGE_GATEWAYHOSTNAME").Value;
            //var domain = configuration.GetSection("DOMAIN_NAME").Value;
            //var fqdn = String.IsNullOrEmpty(domain) ? host : $"{host}.{domain}";
            var blobEndpoint = String.IsNullOrEmpty(storageHost) ? "" : $"BlobEndpoint=http://{storageHost}:{storagePort}/{accountName}";
            connectionString = $"DefaultEndpointsProtocol=http;AccountName={accountName};AccountKey={accountKey};{blobEndpoint}";
        }

        /// <summary>
        /// Task<ActionResult> Index()
        /// Documentation References:
        /// - What is a Storage Account: http://azure.microsoft.com/en-us/documentation/articles/storage-whatis-account/
        /// - Create a Storage Account: https://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/#create-an-azure-storage-account
        /// - Create a Storage Container: https://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/#create-a-container
        /// - List all Blobs in a Storage Container: https://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/#list-the-blobs-in-a-container
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                // Retrieve storage account information from connection string
                // How to create a storage connection string - http://msdn.microsoft.com/en-us/library/azure/ee758697.aspx
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

                // Create a blob client for interacting with the blob service.
                blobClient = storageAccount.CreateCloudBlobClient();
                blobContainer = blobClient.GetContainerReference(blobContainerName);
                await blobContainer.CreateIfNotExistsAsync();

                // To view the uploaded blob in a browser, you have two options. The first option is to use a Shared Access Signature (SAS) token to delegate
                // access to the resource. See the documentation links at the top for more information on SAS. The second approach is to set permissions
                // to allow public access to blobs in this container. Comment the line below to not use this approach and to use SAS. Then you can view the image
                // using: https://[InsertYourStorageAccountNameHere].blob.core.windows.net/webappstoragedotnet-imagecontainer/FileName
                //await blobContainer.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                // Gets all Cloud Block Blobs in the blobContainerName and passes them to teh view
                List<String> allBlobs = new List<String>();
                BlobContinuationToken continuationToken = null;
                do
                {
                    var segment = await blobContainer.ListBlobsSegmentedAsync(continuationToken);

                    foreach (var blob in segment.Results)
                    {
                        if (blob is CloudBlockBlob blockBlob) {
                            allBlobs.Add(blockBlob.Name);
                        }
                    }

                    continuationToken = segment.ContinuationToken;
                }
                while (continuationToken != null);

                return View(allBlobs);
            }
            catch (Exception ex)
            {
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                return Error();
            }
        }

        public async Task<FileStreamResult> GetFile(string blobName)
        {
            var blob = blobContainer.GetBlockBlobReference(blobName);
            var stream = await blob.OpenReadAsync();
            return new FileStreamResult(stream, "image/png");
        }

        private string ConvertToBase64(Stream stream)
        {
            var input = new Byte[(int)stream.Length];
            stream.Read(input, 0, (int)stream.Length);
            var output = Convert.ToBase64String(input, 0, input.Length);
            return output;
        }

        /// <summary>
        /// Task<ActionResult> UploadAsync()
        /// Documentation References:
        /// - UploadFromFileAsync Method: https://msdn.microsoft.com/en-us/library/azure/microsoft.windowsazure.storage.blob.cloudpageblob.uploadfromfileasync.aspx
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UploadAsync()
        {
            try
            {
                var files = Request.Form.Files;
                int fileCount = files.Count;

                if (fileCount > 0)
                {
                    foreach (var file in files)
                    {
                        CloudBlockBlob blob = blobContainer.GetBlockBlobReference(GetRandomBlobName(file.FileName));
                        using (var stream = file.OpenReadStream())
                        {
                            await blob.UploadFromStreamAsync(stream);
                        }
                        blob.Properties.ContentType = "image/png";
                        blob.Metadata["fileName"] = file.FileName;
                        await blob.SetPropertiesAsync();
                        await blob.SetMetadataAsync();
                    }
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                return Error();
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadBlocksAsync()
        {
            Console.WriteLine("UploadBlocksAsync");
            var maxBlockSize = 100000; //100kB
            try
            {
                var files = Request.Form.Files;

                if (files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        if (file.Length == 0) continue;

                        Console.WriteLine($"Reading file {file.Name}");
                        var stream = file.OpenReadStream();
                        var index = 0;
                        var offset = 0;
                        var currentBlockSize = maxBlockSize;
                        var chunks = new Dictionary<string, byte[]>();
                        while (offset < file.Length)
                        {
                            if ((offset + currentBlockSize) >  file.Length)
                            {
                                currentBlockSize = (int)file.Length - offset;
                            }

                            byte[] chunk = new byte[currentBlockSize];
                            Console.WriteLine($"Read chunk at offset {offset} of length {currentBlockSize}");
                            stream.Read(chunk, 0, currentBlockSize);

                            var blockId = Convert.ToBase64String(System.BitConverter.GetBytes(index));
                            chunks[blockId] = chunk;
                            Console.WriteLine($"Created chunk with id {blockId} and size {chunk.Length}");

                            offset += currentBlockSize;
                            index++;
                        }

                        Console.WriteLine($"Uploading file {file.Name}");
                        var blob = blobContainer.GetBlockBlobReference(GetRandomBlobName(file.FileName));
                        foreach (var chunk in chunks)
                        {
                            await blob.PutBlockAsync(chunk.Key, new MemoryStream(chunk.Value), null);
                        }
                        await blob.PutBlockListAsync(chunks.Keys);
                        blob.Properties.ContentType = "image/png";
                        blob.Properties.CacheControl = "public,max-age=60480";
                        blob.Properties.ContentDisposition = "inline";
                        blob.Properties.ContentEncoding = "gzip";
                        blob.Properties.ContentLanguage = "en-US";
                        blob.Metadata["fileName"] = file.FileName;
                        await blob.SetPropertiesAsync();
                        await blob.SetMetadataAsync();
                    }
                }
                return RedirectToAction("Index");
            }
            catch (Exception e)
            {
                ViewData["message"] = e.Message;
                ViewData["trace"] = e.StackTrace;
                return Error();
            }
        }

        /// <summary>
        /// Task<ActionResult> DeleteImage(string name)
        /// Documentation References:
        /// - Delete Blobs: https://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/#delete-blobs
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteImage(string name)
        {
            try
            {
                var blob = blobContainer.GetBlockBlobReference(name);
                await blob.DeleteIfExistsAsync();

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                return Error();
            }
        }

        /// <summary>
        /// Task<ActionResult> DeleteAll(string name)
        /// Documentation References:
        /// - Delete Blobs: https://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/#delete-blobs
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                BlobContinuationToken continuationToken = null;
                do
                {
                    var segment = await blobContainer.ListBlobsSegmentedAsync(continuationToken);

                    foreach (var blob in segment.Results)
                    {
                        if (blob.GetType() == typeof(CloudBlockBlob))
                            await ((CloudBlockBlob)blob).DeleteIfExistsAsync();
                    }

                    continuationToken = segment.ContinuationToken;
                }
                while (continuationToken != null);

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                return Error();
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// string GetRandomBlobName(string filename): Generates a unique random file name to be uploaded
        /// </summary>
        private string GetRandomBlobName(string filename)
        {
            string ext = Path.GetExtension(filename);
            return string.Format("{0:10}_{1}{2}", DateTime.Now.Ticks, Guid.NewGuid(), ext);
        }
    }
}

/*
© CentraleSupelec, 2017
Author: Dr. Jeremy Fix (jeremy.fix@centralesupelec.fr)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
<http://www.apache.org/licenses/LICENSE-2.0>.
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

// Adjustments to new Publication Timing and Execution Framework 
// © Siemens AG, 2018, Dr. Martin Bischoff (martin.bischoff@siemens.com)

using UnityEngine;
using System.Collections.Generic;
using AsyncGPUReadbackPluginNs;

namespace RosSharp.RosBridgeClient
{
    public class ImagePublisher : UnityPublisher<MessageTypes.Sensor.CompressedImage>
    {
        public Camera ImageCamera;
        public string FrameId = "Camera";
        public int resolutionWidth = 640;
        public int resolutionHeight = 480;
        [Range(0, 100)]
        public int qualityLevel = 50;
        public int frameRate = 15;

        private MessageTypes.Sensor.CompressedImage message;
        private Texture2D texture2D;
        private Queue<AsyncGPUReadbackPluginRequest> requests = new Queue<AsyncGPUReadbackPluginRequest>();
        private float updatePeriod = 0.0f;

        protected override void Start()
        {
            base.Start();
            InitializeGameObject();
            InitializeMessage();
            Camera.onPostRender += UpdateImage;
            updatePeriod = 1.0f / (float)frameRate;
        }

        private void Update()
        {
            if(requests.Count > 0)
            {
                var req = requests.Peek();

                // You need to explicitly ask for an update regularly
    			req.Update();

                if (req.hasError)
                {
                    Debug.Log("GPU readback error detected.");
                }
                else if (req.done)
                {
                    UpdateMessage(req);
                }
                else
                {
                    return;
                }
                req.Dispose();
                requests.Dequeue();
            }
            while(requests.Count > 0)
            {
                var req = requests.Peek();
    			req.Update();
                req.GetRawData();
                req.Dispose();
                requests.Dequeue();
            }
        }

        private void InitializeGameObject()
        {
            texture2D = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGBA32, false);
            ImageCamera.targetTexture = new RenderTexture(resolutionWidth, resolutionHeight, 24, RenderTextureFormat.ARGB32);
        }

        private void InitializeMessage()
        {
            message = new MessageTypes.Sensor.CompressedImage();
            message.header.frame_id = FrameId;
            message.format = "jpeg";
        }

        private void UpdateMessage(AsyncGPUReadbackPluginRequest req)
        {
            message.header.Update();
            byte[] buffer = req.GetRawData();
            texture2D.LoadRawTextureData(buffer);
            message.data = texture2D.EncodeToJPG(qualityLevel);
            Publish(message);
        }

        private void UpdateImage(Camera _camera)
        {
            if (Time.frameCount % 3 == 0)
            {
                if (texture2D != null && _camera == this.ImageCamera)
                {
                    Graphics.Blit(null, ImageCamera.targetTexture);
                    requests.Enqueue(AsyncGPUReadbackPlugin.Request(ImageCamera.targetTexture));
                }
            }
        }
    }
}

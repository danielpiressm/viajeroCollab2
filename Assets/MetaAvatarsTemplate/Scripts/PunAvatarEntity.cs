using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Avatar2;
using Photon.Pun;
using System;

namespace Chiligames.MetaAvatarsPun
{
    public class PunAvatarEntity : OvrAvatarEntity
    {
        [SerializeField] StreamLOD streamLOD = StreamLOD.Medium;
        private PhotonView _photonView;
        [SerializeField] List<byte[]> _streamedDataArray = new List<byte[]>();
        [SerializeField] bool useGenericAvatar = false;
        [SerializeField] string avatarPath = "1";
        private int _maxBytesToLog = 5;

        TaskManager taskManager;

        [SerializeField] float _intervalToSendData = 0.08f;
        private float _cycleStartTime = 0;

        private bool skeletonLoaded = false;
        private bool userIDSet;
        public string assetString = "";

        protected override void Awake()
        {
        }

        private void Start()
        {
            _photonView = GetComponent<PhotonView>();
            ConfigureAvatar();
            base.Awake();
            //After entity is created, we can set the remote avatar to be third person (and have a head!)
            if (!_photonView.IsMine)
            {
                SetActiveView(CAPI.ovrAvatar2EntityViewFlags.ThirdPerson);
            }
            if (testingWithoutCredentials)
            {
                LoadLocalAvatar();
            }
            else
            {
                StartCoroutine(TryToLoadUser());
            }
        }

        //Procedurally set the avatar creation features, this needs to be done before base.Awake() to be effective.
        void ConfigureAvatar()
        {
            if (_photonView.IsMine)
            {
                SetIsLocal(true);
                _creationInfo.features = CAPI.ovrAvatar2EntityFeatures.Preset_Default;
                SampleInputManager sampleInputManager = OvrAvatarManager.Instance.gameObject.GetComponent<SampleInputManager>();
                SetBodyTracking(sampleInputManager);
                OvrAvatarLipSyncContext lipSyncInput = FindObjectOfType<OvrAvatarLipSyncContext>();
                SetLipSync(lipSyncInput);
                gameObject.name = "LocalAvatar";
                CollabType collabT = (CollabType)GlobalVariables.Get<int>("collabType");
            }
            else
            {
                SetIsLocal(false);
                _creationInfo.features = CAPI.ovrAvatar2EntityFeatures.Preset_Remote;
                gameObject.name = "RemoteAvatar";
                CollabType collabT = (CollabType) GlobalVariables.Get<int>("collabType");
                if (collabT !=null)
                {
                    if (collabT == CollabType.CoupledView)
                    {
                        foreach(MeshRenderer meshR in gameObject.GetComponentsInChildren<MeshRenderer>())
                        {
                            meshR.enabled = false;
                        }
                    }
                }
                
            }
        }

        IEnumerator TryToLoadUser()
        {
            //We wait until the oculusID is set and the app token has been set
            while (!userIDSet || !OvrAvatarEntitlement.AccessTokenIsValid())
            {
                yield return null;
            }
            var hasAvatarRequest = OvrAvatarManager.Instance.UserHasAvatarAsync(_userId);
            while (hasAvatarRequest.IsCompleted == false)
            {
                yield return null;
            }
            LoadUser();
        }

        //Callback to know when the skeleton was loaded
        protected override void OnSkeletonLoaded()
        {
            base.OnSkeletonLoaded();
            skeletonLoaded = true;
        }

        //If the skeleton is already loaded, we can start streaming the avatar state every "_intervalToSendData" seconds
        private void LateUpdate()
        {
            if (!skeletonLoaded) return;
            float elapsedTime = Time.time - _cycleStartTime;
            if (elapsedTime > _intervalToSendData)
            {
                RecordAndSendStreamDataIfHasAuthority();
                _cycleStartTime = Time.time;
            }
        }

        //We "record" our avatar state and send it to other users, only if avatar is local (is ours)
        void RecordAndSendStreamDataIfHasAuthority()
        {
            if (IsLocal && skeletonLoaded)
            {
                byte[] bytes = RecordStreamData(streamLOD);
                _photonView.RPC("RPC_RecieveStreamData", RpcTarget.Others, bytes);
            }
        }

        [PunRPC]
        public void RPC_RecieveStreamData(byte[] bytes)
        {
            AddToStreamDataList(bytes);
        }

        [PunRPC]
        public void RPC_SetOculusID(long id)
        {
            _userId = (ulong)id;
            userIDSet = true;
        }

        public string GetAssetPath()
        {
            return _assets[0].path;
        }

        //Receive the recorded state of the avatar and add it to the List
        internal void AddToStreamDataList(byte[] bytes)
        {
            if (_streamedDataArray.Count == _maxBytesToLog)
            {
                _streamedDataArray.RemoveAt(_streamedDataArray.Count - 1);
            }
            _streamedDataArray.Add(bytes);
        }

        //If avatar is not local, and the _streamedDataArray has any values in it, we take the first element and apply it to the avatar
        private void Update()
        {
            if (!skeletonLoaded) return;
            if (_streamedDataArray.Count > 0)
            {
                if (!IsLocal)
                {
                    byte[] firstBytesInList = _streamedDataArray[0];
                    if (firstBytesInList != null)
                    {
                        //Apply the remote avatar state and smooth the animation
                        ApplyStreamData(firstBytesInList);
                        SetPlaybackTimeDelay(_intervalToSendData);
                    }
                    _streamedDataArray.RemoveAt(0);
                }
            }
        }

        #region Testing
        public enum AssetSource
        {
            Zip,
            StreamingAssets,
        }
        [Serializable]
        private struct AssetData
        {
            public AssetSource source;
            public string path;
        }
        [Header("Testing in editor")]
        [SerializeField] bool testingWithoutCredentials = false;
        [Tooltip("Filename Postfix (WARNING: Typically the postfix is Platform specific, such as \"_rift.glb\")")]
        [SerializeField] private string _overridePostfix = String.Empty;
        [Tooltip("Adds an underscore between the path and the postfix.")]
        [SerializeField] private bool _underscorePostfix = true;
        [Header("Assets")]
        [Tooltip("Asset paths to load, and whether each asset comes from a preloaded zip file or directly from StreamingAssets")]
        [SerializeField] private List<AssetData> _assets = new List<AssetData> { new AssetData { source = AssetSource.Zip, path = "0" } };
        private bool HasLocalAvatarConfigured => _assets.Count > 0;

        public void LoadNewAvatar(string assetPath)
        {
            if (_assets[0].path == assetPath) return;
            string pathUsed = "";
            if (assetString == "")
                pathUsed = assetPath;
            else
                pathUsed = assetString;
            _photonView.RPC("RPC_SaveAssetPath", RpcTarget.AllBuffered, pathUsed);
            _photonView.RPC("RPC_LoadNewAvatar", RpcTarget.All,pathUsed);
        }

        [PunRPC]
        private void RPC_SaveAssetPath(string assetPath)
        {
            _assets[0] = new AssetData { source = AssetSource.Zip, path = assetPath };
        }

        [PunRPC]
        private void RPC_LoadNewAvatar(string assetPath)
        {
            skeletonLoaded = false;
            EntityActive = false;
            Teardown();
            CreateEntity();
            LoadLocalAvatar();
            assetString = assetPath;
        }

        private void LoadLocalAvatar()
        {
            if (!HasLocalAvatarConfigured)
            {
                Debug.Log("No local avatar asset configured");
                return;
            }
            useGenericAvatar = false;
            if (useGenericAvatar)
            {
                return;
            }

            // Zip asset paths are relative to the inside of the zip.
            // Zips can be loaded from the OvrAvatarManager at startup or by calling OvrAvatarManager.Instance.AddZipSource
            // Assets can also be loaded individually from Streaming assets
            var path = new string[1];
            foreach (var asset in _assets)
            {
                bool isFromZip = (asset.source == AssetSource.Zip);

                string assetPostfix = (_underscorePostfix ? "_" : "")
                    + OvrAvatarManager.Instance.GetPlatformGLBPostfix(isFromZip)
                    + OvrAvatarManager.Instance.GetPlatformGLBVersion(false, isFromZip)
                    + OvrAvatarManager.Instance.GetPlatformGLBExtension(isFromZip);
                if (!String.IsNullOrEmpty(_overridePostfix))
                {
                    assetPostfix = _overridePostfix;
                }
                if (assetString == "")
                    path[0] = asset.path + assetPostfix;
                else
                    path[0] = assetString + assetPostfix;
                if (isFromZip)
                {
                    LoadAssetsFromZipSource(path);
                }
                else
                {
                    LoadAssetsFromStreamingAssets(path);
                }
            }
        }

        public void LoadLocalAvatar(string id)
        {
            if (!HasLocalAvatarConfigured)
            {
                Debug.Log("No local avatar asset configured");
                return;
            }
            useGenericAvatar = false;
            if (useGenericAvatar)
            {
                return;
            }

            
            // Zip asset paths are relative to the inside of the zip.
            // Zips can be loaded from the OvrAvatarManager at startup or by calling OvrAvatarManager.Instance.AddZipSource
            // Assets can also be loaded individually from Streaming assets
            var path = new string[1];
            foreach (var asset in _assets)
            {
                bool isFromZip = (asset.source == AssetSource.Zip);

                string assetPostfix = (_underscorePostfix ? "_" : "")
                    + OvrAvatarManager.Instance.GetPlatformGLBPostfix(isFromZip)
                    + OvrAvatarManager.Instance.GetPlatformGLBVersion(false, isFromZip)
                    + OvrAvatarManager.Instance.GetPlatformGLBExtension(isFromZip);
                if (!String.IsNullOrEmpty(_overridePostfix))
                {
                    assetPostfix = _overridePostfix;
                }

                path[0] = asset.path + assetPostfix;
                path[0] = id + assetPostfix;
                if (isFromZip)
                {
                    LoadAssetsFromZipSource(path);
                }
                else
                {
                    LoadAssetsFromStreamingAssets(path);
                }
            }
        }
        #endregion
    }
}
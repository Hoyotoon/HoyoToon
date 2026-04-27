#if UNITY_EDITOR
using System.Collections.Generic;

namespace HoyoToon.Editor.Resources.Cloudreve
{
    public sealed class CloudreveResponse<TData>
    {
        public CloudreveResponse()
        {
        }

        public TData data;
        public int code;
        public string msg;
        public string error;
        public string correlation_id;
    }

    public sealed class CloudreveShareInfo
    {
        public CloudreveShareInfo()
        {
        }

        public string id;
        public string name;
        public bool unlocked;
        public int source_type;
        public bool expired;
        public string url;
        public bool password_protected;
        public bool is_private;
        public string password;
        public string source_uri;
    }

    public sealed class CloudreveListData
    {
        public CloudreveListData()
        {
        }

        public List<CloudreveFileItem> files;
        public CloudreveFileItem parent;
        public CloudrevePagination pagination;
        public CloudreveNavigatorProps props;
        public string context_hint;
    }

    public sealed class CloudrevePagination
    {
        public CloudrevePagination()
        {
        }

        public int page;
        public int page_size;
        public int total_items;
        public string next_token;
        public bool is_cursor;
    }

    public sealed class CloudreveNavigatorProps
    {
        public CloudreveNavigatorProps()
        {
        }

        public int max_page_size;
    }

    public sealed class CloudreveFileItem
    {
        public CloudreveFileItem()
        {
        }

        public int type;
        public string id;
        public string name;
        public string created_at;
        public string updated_at;
        public long size;
        public Dictionary<string, string> metadata;
        public string path;
        public string primary_entity;
    }

    public sealed class CloudreveDownloadUrlRequest
    {
        public CloudreveDownloadUrlRequest()
        {
        }

        public List<string> uris = new List<string>();
        public bool download = true;
        public bool redirect;
        public bool archive;
        public bool no_cache = true;
        public bool use_primary_site_url = true;
    }

    public sealed class CloudreveDownloadUrlData
    {
        public CloudreveDownloadUrlData()
        {
        }

        public List<CloudreveDownloadUrlItem> urls;
        public string expires;
    }

    public sealed class CloudreveDownloadUrlItem
    {
        public CloudreveDownloadUrlItem()
        {
        }

        public string url;
        public string stream_saver_display_name;
    }
}
#endif
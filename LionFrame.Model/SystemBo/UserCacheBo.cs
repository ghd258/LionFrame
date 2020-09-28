﻿using System;
using System.Collections.Generic;

namespace LionFrame.Model.SystemBo
{
    public class UserCacheBo
    {
        public long UserId { get; set; }
        public string UserName { get; set; }
        public string PassWord { get; set; }
        public int Sex { get; set; }
        public string Email { get; set; }
        public string UserToken { get; set; }
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; }
        public string LoginIp { get; set; }
        public DateTime LoginTime { get; set; }
        public IEnumerable<RoleCacheBo> RoleCacheBos { get; set; }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace LionFrame.Config
{
    /// <summary>
    /// 缓存key值  统一管理
    /// </summary>
    public static class CacheKeys
    {
        /// <summary>
        /// 用户信息缓存  后面接用户ID
        /// </summary>
        public static readonly string USER = "U_User_";
        /// <summary>
        /// 菜单缓存 后接用户id
        /// </summary>
        public static readonly string MENU_TREE = "MenuTree_";

    }
}
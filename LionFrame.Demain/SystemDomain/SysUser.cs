﻿using LionFrame.Domain.BaseDomain;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LionFrame.Domain.SystemDomain
{
    [Table("Sys_User")]
    public class SysUser : BaseModel
    {
        [Key]
        public long UserId { get; set; }

        [Column(TypeName = "varchar(128)"), Required]
        public string UserName { get; set; }

        [Column(TypeName = "varchar(512)"), Required]
        public string PassWord { get; set; }

        [Column(TypeName = "varchar(256)"), Required]
        [DefaultValue("")]
        public string Email { get; set; }

        /// <summary>
        /// 男女 1男 0女
        /// </summary>
        [DefaultValue(1)]
        public int Sex { get; set; }
        /// <summary>
        /// 1为用户有效状态 -1为无效 其它尚未定义
        /// </summary>
        public int Status { get; set; }

        [Column(TypeName = "datetime2(7)")]
        public DateTime CreatedTime { get; set; }

        [Column(TypeName = "datetime2(7)")]
        public DateTime UpdatedTime { get; set; }

        public List<SysUserRoleRelation> SysUserRoleRelations { get; set; }
    }
}

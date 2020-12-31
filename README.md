# PixivAss
P站本地助手，预下载图片到本地，标记看过的图，定时搜索可能感兴趣的图生成浏览队列，本地收藏图片(可分P收藏)和关注。

# 运行前需要
在config.json填写用户名和用户id(用于RunInitTask,不影响其它)

在config.json指定的地址(默认为本地1081端口)运行可访问pixiv的代理

在config.json指定的地址(默认本地4321端口)运行mysql server

用create_table.sql初始化msyql数据库

用premium会员账号登录pixiv(premium会员用于关键字搜索时提供排序权限，不影响其它)

在chrome上登录pixiv并运行pixivHelper插件(用于向程序发送cookie)，或手动把cookie填到数据库的status表中

用nuget还原依赖库



# 爬虫附带简易图片浏览和库存管理
1.UI
2.Pixiv
3.Hitomi.la
4.本地图片
5.Kemono
6.Pawchive
7.Manhuagui
8.Hentaiera
9.Telegram

Telegram 支持通过数据库维护屏蔽词，在下载前检查相册和评论套图用于命名的短标题（说明先排除裸链接，再截取前30个字符并清理，保留带链接的可见文字），以及 Telegraph 消息正文和预览标题。匹配忽略大小写；独立散图和已下载文件不受影响。每轮下载开始时读取最新屏蔽词，命中的消息会永久忽略。移除屏蔽词不会自动恢复之前忽略的消息。

AuthorHub 支持手动关联 Pixiv、X、Hitomi、Kemono 和 Pawchive 中的同一作者，通过统一队列浏览图片。需准备独立数据库并配置 `AuthorHubConnectStr`。

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

Pawchive 支持按账号选择附件图片、附件压缩包图片、视频及封面下载，并可启用标题优先级规则，减少同名帖子不同版本的重复下载。纯图片 ZIP、RAR 和 7z 压缩包下载后各自形成独立图片组。

LSF 支持按文件夹浏览本地图片和逐页收藏。已收藏的图片保留，已读且未收藏的图片由定期清理删除。

Telegram 支持对相册、评论套图和 Telegraph 消息进行屏蔽词过滤。命中的消息会永久忽略，移除屏蔽词不会自动恢复之前忽略的消息。

配置作者匹配词库后，Telegram 提交的 Telegraph 下载任务按预览标题匹配作者，匹配成功时保存到“作者／作品”目录，未匹配或待确认时沿用原目录规则。需要下载服务支持指定目录。

AuthorHub 支持手动关联 Pixiv、X、Hitomi、Kemono 和 Pawchive 中的同一作者，通过统一队列浏览图片。需准备独立数据库并配置 `AuthorHubConnectStr`；Pixiv、Pawchive、Hitomi、X 和 Kemono 的统一收藏根目录通过 `AuthorHubFavDir` 配置。

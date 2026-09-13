# Pawchive

Based on the Kemono module, with a separate database, file directory and aria2 process.

- Configure `PawchiveConnectStr` and `PawchiveDownloadDir`. An empty connection string disables this module.
- Apply the Pawchive EF migrations manually before enabling the module.
- Release runs the existing daily fetch and 30-minute download schedule. Debug only applies pending UI operations.
- Patreon and Fanbox use the user/post API inherited from Kemono. Discord channel fetching is not implemented in Kemono or this initial port.

## API Differences

- Profiles omit `post_count`. Pagination ends on an empty page or fewer than 50 posts.
- Post details return the post directly, without a `post` wrapper.
- `has_full=false` means previews only. These groups remain unfetched and are checked again by the daily task; previews are not saved as originals.
- Original files use `https://file.pawchive.pw/data/...`, including the webpage's `f` filename parameter.
- The file server rejects browser impersonation by download tools. File downloads identify the bundled aria2 1.33.0; API requests identify PictureSpider with its project URL.
- Requests and download submissions are spaced by two seconds. Each batch contains at most five downloads, with one connection per file.

## Verification

The existing Kemono database sample `fanbox/7349257` was fetched into the separate Pawchive database: 142 groups and 349 work records. Post `12473822` downloaded a 3081 x 3019 PNG (10,976,605 bytes). Its SHA-256 matches the hash in the source URL. No other module's schedule was started.

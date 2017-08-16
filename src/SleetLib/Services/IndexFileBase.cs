using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Sleet
{
    public abstract class IndexFileBase
    {
        protected SleetContext Context { get; }

        protected ISleetFile File { get; }

        protected bool PersistWhenEmpty { get; }

        protected IndexFileBase(SleetContext context, string path, bool persistWhenEmpty)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            File = context.Source.Get(path);
            PersistWhenEmpty = persistWhenEmpty;
        }

        protected IndexFileBase(SleetContext context, ISleetFile file, bool persistWhenEmpty)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            File = file ?? throw new ArgumentNullException(nameof(file));
            PersistWhenEmpty = persistWhenEmpty;
        }

        /// <summary>
        /// Returned when the file does not exist.
        /// </summary>
        /// <returns></returns>
        protected virtual Task<JObject> GetJsonTemplateAsync()
        {
            return Task.FromResult(new JObject());
        }

        /// <summary>
        /// Read file from disk.
        /// </summary>
        protected virtual async Task<JObject> GetJsonOrTemplateAsync()
        {
            var file = File;

            if (await file.Exists(Context.Log, Context.Token))
            {
                return await file.GetJson(Context.Log, Context.Token);
            }
            else
            {
                return await GetJsonTemplateAsync();
            }
        }

        /// <summary>
        /// Save file to disk.
        /// </summary>
        protected virtual Task SaveAsync(JObject json, bool isEmpty)
        {
            var file = File;

            if (isEmpty && !PersistWhenEmpty)
            {
                // Remove the empty file
                file.Delete(Context.Log, Context.Token);

                return Task.FromResult(true);
            }
            else
            {
                // Write the file to disk
                return file.Write(json, Context.Log, Context.Token);
            }
        }

        /// <summary>
        /// Create a new file if the file does not exist.
        /// </summary>
        public async Task InitAsync()
        {
            if (await File.Exists(Context.Log, Context.Token) == false)
            {
                var json = await GetJsonOrTemplateAsync();
                await SaveAsync(json, isEmpty: false);
            }
        }
    }
}

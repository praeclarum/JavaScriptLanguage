namespace Reflector.Application.Languages
{
    using System;
    using System.ComponentModel;

    internal class JavaScriptLanguagePackage : IPackage
    {
        private ILanguageManager languageManager;
        private JavaScriptLanguage delphiLanguage;

        public void Load(IServiceProvider serviceProvider)
        {
            this.delphiLanguage = new JavaScriptLanguage(true);
            //this.delphiLanguage.VisibilityConfiguration = (IVisibilityConfiguration) serviceProvider.GetService(typeof(IVisibilityConfiguration));
            //this.delphiLanguage.FormatterConfiguration = (IFormatterConfiguration) serviceProvider.GetService(typeof(IFormatterConfiguration));

            this.languageManager = (ILanguageManager)serviceProvider.GetService(typeof(ILanguageManager));

            for (int i = this.languageManager.Languages.Count - 1; i >= 0; i--)
            {
                if (this.languageManager.Languages[i].Name == "JavaScript")
                {
                    this.languageManager.UnregisterLanguage(this.languageManager.Languages[i]);
                }
            }

            this.languageManager.RegisterLanguage(this.delphiLanguage);
        }

        public void Unload()
        {
            this.languageManager.UnregisterLanguage(this.delphiLanguage);
        }
    }
}

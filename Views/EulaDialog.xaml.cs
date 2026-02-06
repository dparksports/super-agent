using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;

namespace OpenClaw.Windows.Views
{
    public sealed partial class EulaDialog : ContentDialog
    {
        public EulaDialog()
        {
            this.InitializeComponent();
            LoadTerms();
        }

        private void LoadTerms()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "terms.txt");
                if (File.Exists(path))
                {
                    TermsTextBlock.Text = File.ReadAllText(path);
                }
                else
                {
                    TermsTextBlock.Text = "Terms of Service file not found.";
                }
            }
            catch (Exception ex)
            {
                TermsTextBlock.Text = "Error loading terms: " + ex.Message;
            }
        }
    }
}

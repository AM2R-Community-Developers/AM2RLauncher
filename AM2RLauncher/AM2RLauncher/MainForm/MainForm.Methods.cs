using AM2RLauncher.Core;
using AM2RLauncher.Language;
using Eto.Forms;
using LibGit2Sharp;
using System;

namespace AM2RLauncher
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Method that updates <see cref="progressBar"/>.
        /// </summary>
        /// <param name="value">The value that <see cref="progressBar"/> should be set to.</param>
        /// <param name="min">The min value that <see cref="progressBar"/> should be set to.</param>
        /// <param name="max">The max value that <see cref="progressBar"/> should be set to.</param>
        private void UpdateProgressBar(int value, int min = 0, int max = 100)
        {
            Application.Instance.Invoke(() =>
            {
                progressBar.MinValue = min;
                progressBar.MaxValue = max;
                progressBar.Value = value;
            });
        }


        /// <summary>
        /// Method that updates <see cref="progressBar"/> with a min value of 0 and max value of 100.
        /// </summary>
        /// <param name="value">The value that <see cref="progressBar"/> should be set to.</param>
        private void UpdateProgressBar(int value)
        {
            UpdateProgressBar(value, 0, 100);
        }

        /// <summary>
        /// Safety check function before accessing <see cref="profileIndex"/>.
        /// </summary>
        /// <returns><see langword="true"/> if it is valid, <see langword="false"/> if not.</returns>
        private bool IsProfileIndexValid()
        {
            return profileIndex != null;
        }

        /// <summary>
        /// This is just a helper method for the git commands in order to have a progress bar display for them.
        /// </summary>
        private bool TransferProgressHandlerMethod(TransferProgress transferProgress)
        {
            // Thank you random issue on the gitlib2sharp repo!!!!
            // Also tldr; rtfm
            if (isGitProcessGettingCancelled) return false;

            // This needs to be in an Invoke, in order to access the variables from the main thread
            // Otherwise this will throw a runtime exception
            Application.Instance.Invoke(() =>
            {
                progressBar.MinValue = 0;
                progressBar.MaxValue = transferProgress.TotalObjects;
                if (currentGitObject >= transferProgress.ReceivedObjects)
                    return;
                progressLabel.Text = Text.ProgressbarProgress + " " + transferProgress.ReceivedObjects + " (" + ((int)transferProgress.ReceivedBytes / 1000000) + "MB) / " + transferProgress.TotalObjects + " objects";
                currentGitObject = transferProgress.ReceivedObjects;
                progressBar.Value = transferProgress.ReceivedObjects;
            });

            return true;
        }

        /// <summary>
        /// Creates a single-file, zip-filtered file dialog.
        /// </summary>
        /// <param name="title">The title of the file dialog.</param>
        /// <returns>The created file dialog.</returns>
        private OpenFileDialog GetSingleZipDialog(string title = "")
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Directory = new Uri(CrossPlatformOperations.CURRENTPATH),
                MultiSelect = false,
                Title = title
            };
            fileDialog.Filters.Add(new FileFilter(Text.ZipArchiveText, ".zip"));
            return fileDialog;
        }

        private void DisableProgressBar()
        {
            progressBar.Visible = false;
            progressBar.Value = 0;
        }

        private void EnableProgressBar()
        {
            progressBar.Visible = true;
            progressBar.Value = 0;
        }

        private void DisableProgressBarAndProgressLabel()
        {
            DisableProgressBar();
            progressLabel.Visible = false;
            progressLabel.Text = "";
        }

        private void EnableProgressBarAndLabel()
        {
            EnableProgressBar();
            progressLabel.Visible = true;
            progressLabel.Text = "";
        }
    }
}